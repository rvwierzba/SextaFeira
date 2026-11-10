using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SextaFeira.Domain.Agent;
using SextaFeira.Infrastructure.Storage;

namespace SextaFeira.Infrastructure.Mcp;

/// <summary>Uma entrada de data/mcp.json (mesmo formato do Claude Desktop / Cursor / VS Code).</summary>
public sealed class McpServerConfig
{
    public string? Command { get; set; }
    public List<string>? Args { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public bool Disabled { get; set; }
}

public sealed record McpServerStatus(string Name, string Transport, string State, string Detail, int ToolCount, string? PendingLoginUrl);

/// <summary>
/// Conecta aos servidores MCP declarados em data/mcp.json e expõe as ferramentas deles para a assistente.
/// Servidores remotos (HTTP) com OAuth abrem o login no navegador do computador uma única vez; os tokens ficam salvos.
/// </summary>
public sealed class McpManager : IToolProvider, IHostedService, IAsyncDisposable
{
    private const int OAuthCallbackPort = 7891;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    private readonly ILogger<McpManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, Connection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    private sealed class Connection
    {
        public required string Name { get; init; }
        public required string Transport { get; init; }
        public McpClient? Client { get; set; }
        public List<ITool> Tools { get; set; } = [];
        public string State { get; set; } = "conectando";
        public string Detail { get; set; } = "";
        public string? PendingLoginUrl { get; set; }
    }

    public McpManager(ILogger<McpManager> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public event Action? Changed;

    public IReadOnlyList<ITool> GetTools() => _connections.Values.SelectMany(c => c.Tools).ToList();

    public IReadOnlyList<McpServerStatus> GetStatus() =>
        _connections.Values.OrderBy(c => c.Name)
            .Select(c => new McpServerStatus(c.Name, c.Transport, c.State, c.Detail, c.Tools.Count, c.PendingLoginUrl)).ToList();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        EnsureConfigExists();
        _ = Task.Run(() => ReloadAsync(CancellationToken.None), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) => await DisposeAsync();

    public string ReadConfigText() => File.Exists(DataPaths.McpConfig) ? File.ReadAllText(DataPaths.McpConfig) : "";

    public async Task SaveConfigTextAsync(string json, CancellationToken ct)
    {
        // Valida antes de salvar.
        _ = ParseConfig(json);
        await File.WriteAllTextAsync(DataPaths.McpConfig, json, ct);
        await ReloadAsync(ct);
    }

    public async Task ReloadAsync(CancellationToken ct)
    {
        await _reloadLock.WaitAsync(ct);
        try
        {
            foreach (var c in _connections.Values)
                if (c.Client != null) await SafeDispose(c.Client);
            _connections.Clear();

            Dictionary<string, McpServerConfig> servers;
            try { servers = ParseConfig(ReadConfigText()); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "data/mcp.json inválido.");
                return;
            }

            var tasks = servers.Where(s => !s.Value.Disabled).Select(s => ConnectAsync(s.Key, s.Value, ct));
            Changed?.Invoke();
            await Task.WhenAll(tasks);
        }
        finally
        {
            _reloadLock.Release();
            Changed?.Invoke();
        }
    }

    private async Task ConnectAsync(string name, McpServerConfig config, CancellationToken ct)
    {
        var conn = new Connection { Name = name, Transport = config.Url != null ? "http" : "stdio" };
        _connections[name] = conn;

        try
        {
            IClientTransport transport = config.Url != null
                ? new HttpClientTransport(new HttpClientTransportOptions
                {
                    Name = name,
                    Endpoint = new Uri(config.Url),
                    AdditionalHeaders = config.Headers,
                    OAuth = new ClientOAuthOptions
                    {
                        RedirectUri = new Uri($"http://localhost:{OAuthCallbackPort}/callback"),
#pragma warning disable MCP9007 // O substituto (AuthorizationCallbackHandler) ainda muda entre versões do SDK; o fluxo atual funciona.
                        AuthorizationRedirectDelegate = (authUri, redirectUri, token) => HandleBrowserLoginAsync(conn, authUri, redirectUri, token),
#pragma warning restore MCP9007
                        DynamicClientRegistration = new() { ClientName = "Sexta-Feira" },
                        TokenCache = new FileTokenCache(Path.Combine(DataPaths.McpTokens, Sanitize(name) + ".json")),
                    },
                }, _loggerFactory)
                : new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = name,
                    Command = config.Command ?? throw new InvalidOperationException("Faltou 'command' ou 'url'."),
                    Arguments = config.Args ?? [],
                    EnvironmentVariables = config.Env?.ToDictionary(kv => kv.Key, kv => (string?)kv.Value),
                }, _loggerFactory);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(3)); // inclui o tempo do login no navegador
            var client = await McpClient.CreateAsync(transport, new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "sexta-feira", Version = "3.0" }
            }, _loggerFactory, timeout.Token);

            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
            conn.Client = client;
            conn.Tools = tools.Select(t => (ITool)new McpTool(name, t)).ToList();
            conn.State = "conectado";
            conn.Detail = $"{tools.Count} ferramentas";
            conn.PendingLoginUrl = null;
            _logger.LogInformation("MCP {Server}: {Count} ferramentas.", name, tools.Count);
        }
        catch (Exception ex)
        {
            conn.State = "erro";
            conn.Detail = ex.InnerException?.Message ?? ex.Message;
            _logger.LogWarning(ex, "Falha ao conectar no servidor MCP {Server}", name);
        }
        Changed?.Invoke();
    }

    /// <summary>Abre o login OAuth no navegador do computador e espera o retorno em http://localhost:7891/callback.</summary>
    private async Task<string?> HandleBrowserLoginAsync(Connection conn, Uri authorizationUri, Uri redirectUri, CancellationToken ct)
    {
        conn.State = "aguardando login";
        conn.PendingLoginUrl = authorizationUri.ToString();
        conn.Detail = "Faça login na janela do navegador que abriu no computador.";
        Changed?.Invoke();

        using var listener = new HttpListener();
        listener.Prefixes.Add($"{redirectUri.GetLeftPart(UriPartial.Authority)}/");
        listener.Start();
        try { Process.Start(new ProcessStartInfo(authorizationUri.ToString()) { UseShellExecute = true }); }
        catch (Exception ex) { _logger.LogWarning(ex, "Não consegui abrir o navegador; use o link no HUD."); }

        while (!ct.IsCancellationRequested)
        {
            var ctxTask = listener.GetContextAsync();
            var done = await Task.WhenAny(ctxTask, Task.Delay(Timeout.Infinite, ct));
            if (done != ctxTask) break;
            var ctx = await ctxTask;
            var code = ctx.Request.QueryString["code"];
            var error = ctx.Request.QueryString["error"];
            var html = code != null
                ? "<h2>Pronto! A Sexta-Feira já está conectada. Pode fechar esta aba.</h2>"
                : $"<h2>Login não concluído: {WebUtility.HtmlEncode(error ?? "sem código")}</h2>";
            var bytes = Encoding.UTF8.GetBytes($"<html><body style='font-family:sans-serif;background:#050a14;color:#cfefff'>{html}</body></html>");
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.OutputStream.WriteAsync(bytes, ct);
            ctx.Response.Close();
            if (code != null || error != null)
            {
                conn.PendingLoginUrl = null;
                conn.State = "conectando";
                Changed?.Invoke();
                return code;
            }
        }
        return null;
    }

    private static Dictionary<string, McpServerConfig> ParseConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var servers = root?["mcpServers"] ?? root?["servers"];
        if (servers is not JsonObject obj) return [];
        var result = new Dictionary<string, McpServerConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in obj)
        {
            if (key.StartsWith("_") || value is null) continue; // "_exemplo": comentários/modelos
            result[key] = value.Deserialize<McpServerConfig>(JsonOptions) ?? new();
        }
        return result;
    }

    private static void EnsureConfigExists()
    {
        Directory.CreateDirectory(DataPaths.McpTokens);
        if (File.Exists(DataPaths.McpConfig)) return;
        File.WriteAllText(DataPaths.McpConfig, """
            {
              // Servidores MCP no mesmo formato do Claude Desktop / Cursor / VS Code.
              // Chaves que começam com "_" são ignoradas: renomeie (tire o "_") para ativar.
              // Servidores remotos (url) com OAuth abrem o login no navegador uma vez e guardam o token.
              "mcpServers": {
                "_arquivos": {
                  "command": "npx",
                  "args": ["-y", "@modelcontextprotocol/server-filesystem", "C:\\Users\\SEU_USUARIO\\Documents"]
                },
                "_github": {
                  "url": "https://api.githubcopilot.com/mcp/"
                },
                "_navegador": {
                  "command": "npx",
                  "args": ["-y", "@playwright/mcp@latest"]
                }
              }
            }
            """);
    }

    internal static string Sanitize(string s) => Regex.Replace(s, "[^a-zA-Z0-9_-]", "_");

    private static async Task SafeDispose(McpClient client)
    {
        try { await client.DisposeAsync(); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _connections.Values)
            if (c.Client != null) await SafeDispose(c.Client);
        _connections.Clear();
    }
}

/// <summary>Adapta uma ferramenta MCP para o contrato interno <see cref="ITool"/>.</summary>
internal sealed class McpTool : ITool
{
    private readonly McpClientTool _tool;

    public McpTool(string server, McpClientTool tool)
    {
        _tool = tool;
        Source = "mcp:" + server;
        var name = $"{McpManager.Sanitize(server)}__{McpManager.Sanitize(tool.Name)}";
        if (name.Length > 64) name = name[..64];
        var schema = tool.JsonSchema.ValueKind == JsonValueKind.Object
            ? tool.JsonSchema
            : JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement;
        Spec = new ToolSpec(name, string.IsNullOrWhiteSpace(tool.Description) ? tool.Name : tool.Description, schema);
    }

    public ToolSpec Spec { get; }
    public string Source { get; }

    public async Task<string> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct)
    {
        var args = new Dictionary<string, object?>();
        if (arguments.ValueKind == JsonValueKind.Object)
            foreach (var p in arguments.EnumerateObject()) args[p.Name] = p.Value.Clone();

        var result = await _tool.CallAsync(args, cancellationToken: ct);
        var sb = new StringBuilder();
        foreach (var block in result.Content)
        {
            switch (block)
            {
                case TextContentBlock t: sb.AppendLine(t.Text); break;
                case ImageContentBlock: sb.AppendLine("[imagem]"); break;
                default: sb.AppendLine($"[{block.Type}]"); break;
            }
        }
        var text = sb.ToString().Trim();
        return result.IsError == true ? $"Erro: {text}" : (text.Length == 0 ? "OK." : text);
    }
}
