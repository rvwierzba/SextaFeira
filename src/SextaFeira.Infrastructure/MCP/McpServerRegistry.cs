using System.Text.Json;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Infrastructure.MCP;

public class McpServerRegistry : IMcpHost
{
    private readonly INetworkScanner _networkScanner;
    private readonly IStealthBrowser _stealthBrowser;
    private readonly ISandboxService _sandboxService;
    private readonly IPersonalIntegrationsService _personalIntegrations;
    private readonly IGeolocationService _geolocationService;
    private readonly ISelfEvolutionService _selfEvolutionService;
    private readonly ILogger<McpServerRegistry> _logger;

    public McpServerRegistry(
        INetworkScanner networkScanner,
        IStealthBrowser stealthBrowser,
        ISandboxService sandboxService,
        IPersonalIntegrationsService personalIntegrations,
        IGeolocationService geolocationService,
        ISelfEvolutionService selfEvolutionService,
        ILogger<McpServerRegistry> logger)
    {
        _networkScanner = networkScanner;
        _stealthBrowser = stealthBrowser;
        _sandboxService = sandboxService;
        _personalIntegrations = personalIntegrations;
        _geolocationService = geolocationService;
        _selfEvolutionService = selfEvolutionService;
        _logger = logger;
    }

    public Task<List<ToolDefinition>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<ToolDefinition>
        {
            new(
                Name: "network_scan",
                Description: "Escaneia a rede local (Wi-Fi/Ethernet) e retorna os nós e dispositivos ativos.",
                ParametersJsonSchema: "{}",
                Category: ToolCategory.Network
            ),
            new(
                Name: "stealth_search",
                Description: "Realiza busca furtiva na web com evasão anti-bot e extrai conteúdo em Markdown.",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}",
                Category: ToolCategory.StealthBrowser
            ),
            new(
                Name: "stealth_extract_url",
                Description: "Acessa uma página web em modo stealth e extrai o conteúdo limpo do DOM.",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}",
                Category: ToolCategory.StealthBrowser
            ),
            new(
                Name: "docker_execute_code",
                Description: "Executa código isolado dentro de um container Docker temporário (Zero-Trust).",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"language\":{\"type\":\"string\"},\"code\":{\"type\":\"string\"}},\"required\":[\"language\",\"code\"]}",
                Category: ToolCategory.Sandbox
            ),
            new(
                Name: "calendar_get_upcoming",
                Description: "Obtém os próximos compromissos da agenda do usuário no Google Calendar.",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"count\":{\"type\":\"integer\"}}}",
                Category: ToolCategory.Calendar
            ),
            new(
                Name: "email_get_recent",
                Description: "Obtém os e-mails recentes não lidos da caixa de entrada do Gmail / Outlook.",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"count\":{\"type\":\"integer\"}}}",
                Category: ToolCategory.Email
            ),
            new(
                Name: "google_gmail_send",
                Description: "Envia e-mail formatado usando o conector MCP de e-mail do Google / Gmail.",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"recipient\":{\"type\":\"string\"},\"subject\":{\"type\":\"string\"},\"body\":{\"type\":\"string\"}},\"required\":[\"recipient\",\"subject\",\"body\"]}",
                Category: ToolCategory.GoogleServices
            ),
            new(
                Name: "spotify_get_current",
                Description: "Obtém o status de reprodução e a música atual sendo tocada no Spotify.",
                ParametersJsonSchema: "{}",
                Category: ToolCategory.Spotify
            ),
            new(
                Name: "spotify_play",
                Description: "Reproduz uma música, artista ou playlist no Spotify.",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}",
                Category: ToolCategory.Spotify
            ),
            new(
                Name: "spotify_search",
                Description: "Pesquisa por faixas, álbuns ou artistas no catálogo do Spotify.",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}",
                Category: ToolCategory.Spotify
            ),
            new(
                Name: "onedrive_list_files",
                Description: "Lista diretórios e arquivos armazenados no OneDrive sincronizado.",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}}}",
                Category: ToolCategory.Files
            ),
            new(
                Name: "get_geolocation_weather",
                Description: "Obtém as coordenadas de geolocalização e previsão do tempo atual para Piracaia, SP e região.",
                ParametersJsonSchema: "{}",
                Category: ToolCategory.Geolocation
            ),
            new(
                Name: "self_evolve_codebase",
                Description: "Executa a auto-modificação/evolução do próprio código da Sexta-Feira para criar novas funções ou reparar funcionalidades.",
                ParametersJsonSchema: "{\"type\":\"object\",\"properties\":{\"instruction\":{\"type\":\"string\"},\"targetFile\":{\"type\":\"string\"}},\"required\":[\"instruction\"]}",
                Category: ToolCategory.CodeEvolution
            )
        };

        return Task.FromResult(tools);
    }

    public async Task<ToolExecutionResult> DispatchToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Despachando ferramenta MCP: {ToolName}", toolName);

        try
        {
            switch (toolName.ToLowerInvariant())
            {
                case "network_scan":
                    var devices = await _networkScanner.ScanLocalNetworkAsync(cancellationToken);
                    return new ToolExecutionResult(toolName, true, JsonSerializer.Serialize(devices));

                case "stealth_search":
                    using (var doc = JsonDocument.Parse(argumentsJson))
                    {
                        var query = doc.RootElement.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                        var results = await _stealthBrowser.SearchWebStealthAsync(query, cancellationToken);
                        return new ToolExecutionResult(toolName, true, results);
                    }

                case "stealth_extract_url":
                    using (var doc = JsonDocument.Parse(argumentsJson))
                    {
                        var url = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                        var markdown = await _stealthBrowser.NavigateAndExtractMarkdownAsync(url, cancellationToken);
                        return new ToolExecutionResult(toolName, true, markdown);
                    }

                case "docker_execute_code":
                    using (var doc = JsonDocument.Parse(argumentsJson))
                    {
                        var lang = doc.RootElement.TryGetProperty("language", out var l) ? l.GetString() ?? "python" : "python";
                        var code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
                        var job = await _sandboxService.ExecuteCodeAsync(lang, code, TimeSpan.FromSeconds(30), cancellationToken);
                        return new ToolExecutionResult(toolName, job.Status == ExecutionStatus.Success, JsonSerializer.Serialize(job));
                    }

                case "calendar_get_upcoming":
                    var events = await _personalIntegrations.GetUpcomingEventsAsync(5, cancellationToken);
                    return new ToolExecutionResult(toolName, true, JsonSerializer.Serialize(events));

                case "email_get_recent":
                    var emails = await _personalIntegrations.GetRecentEmailsAsync(5, cancellationToken);
                    return new ToolExecutionResult(toolName, true, JsonSerializer.Serialize(emails));

                case "google_gmail_send":
                    using (var doc = JsonDocument.Parse(argumentsJson))
                    {
                        var recipient = doc.RootElement.TryGetProperty("recipient", out var r) ? r.GetString() ?? "" : "";
                        var subject = doc.RootElement.TryGetProperty("subject", out var s) ? s.GetString() ?? "" : "";
                        var body = doc.RootElement.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                        var sent = await _personalIntegrations.SendEmailAsync(recipient, subject, body, cancellationToken);
                        return new ToolExecutionResult(toolName, sent, sent ? "E-mail enviado com sucesso." : "Falha ao enviar e-mail.");
                    }

                case "spotify_get_current":
                    var currentTrack = await _personalIntegrations.GetCurrentSpotifyPlaybackAsync(cancellationToken);
                    return new ToolExecutionResult(toolName, true, JsonSerializer.Serialize(currentTrack));

                case "spotify_play":
                    using (var doc = JsonDocument.Parse(argumentsJson))
                    {
                        var query = doc.RootElement.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                        var played = await _personalIntegrations.PlaySpotifyTrackAsync(query, cancellationToken);
                        return new ToolExecutionResult(toolName, played, $"Reproduzindo no Spotify: {query}");
                    }

                case "spotify_search":
                    using (var doc = JsonDocument.Parse(argumentsJson))
                    {
                        var query = doc.RootElement.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                        var tracks = await _personalIntegrations.SearchSpotifyTracksAsync(query, 5, cancellationToken);
                        return new ToolExecutionResult(toolName, true, JsonSerializer.Serialize(tracks));
                    }

                case "onedrive_list_files":
                    var files = await _personalIntegrations.GetOneDriveFilesAsync("/", cancellationToken);
                    return new ToolExecutionResult(toolName, true, JsonSerializer.Serialize(files));

                case "get_geolocation_weather":
                    double? clientLat = null;
                    double? clientLon = null;
                    if (!string.IsNullOrWhiteSpace(argumentsJson) && argumentsJson != "{}")
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(argumentsJson);
                            if (doc.RootElement.TryGetProperty("latitude", out var latEl)) clientLat = latEl.GetDouble();
                            if (doc.RootElement.TryGetProperty("longitude", out var lonEl)) clientLon = lonEl.GetDouble();
                        }
                        catch { }
                    }
                    var liveGeoWeather = await _geolocationService.GetLiveLocationAndWeatherAsync(clientLat, clientLon, cancellationToken);
                    return new ToolExecutionResult(toolName, true, JsonSerializer.Serialize(liveGeoWeather));

                case "self_evolve_codebase":
                    using (var doc = JsonDocument.Parse(argumentsJson))
                    {
                        var instruction = doc.RootElement.TryGetProperty("instruction", out var i) ? i.GetString() ?? "" : "";
                        var targetFile = doc.RootElement.TryGetProperty("targetFile", out var tf) ? tf.GetString() : null;
                        var result = await _selfEvolutionService.EvolveCodebaseAsync(instruction, targetFile, cancellationToken);
                        return new ToolExecutionResult(toolName, result.Succeeded, JsonSerializer.Serialize(result));
                    }

                default:
                    return new ToolExecutionResult(toolName, false, "Ferramenta não reconhecida no registro MCP.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na execução da ferramenta {ToolName}", toolName);
            return new ToolExecutionResult(toolName, false, string.Empty, ex.Message);
        }
    }
}
