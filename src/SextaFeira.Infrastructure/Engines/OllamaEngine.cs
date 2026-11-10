using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Agent;

namespace SextaFeira.Infrastructure.Engines;

/// <summary>
/// Engine local via Ollama (/api/chat) com streaming e tool calling nativo.
/// </summary>
public sealed class OllamaEngine : IChatEngine
{
    private readonly IHttpClientFactory _http;
    private readonly ISettingsStore _settings;
    private readonly ILogger<OllamaEngine> _logger;

    public OllamaEngine(IHttpClientFactory http, ISettingsStore settings, ILogger<OllamaEngine> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    public string Id => "ollama";
    public string DisplayName => "Ollama (local)";
    public bool IsLocal => true;
    public string CurrentModel => _settings.Current.Ollama.Model;

    private string BaseUrl => _settings.Current.Ollama.BaseUrl.TrimEnd('/');

    public async Task<EngineHealth> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = _http.CreateClient("probe");
            var tags = await client.GetFromJsonAsync<JsonElement>($"{BaseUrl}/api/tags", ct);
            var models = tags.GetProperty("models").EnumerateArray()
                .Select(m => m.GetProperty("name").GetString() ?? "")
                .Where(n => n.Length > 0).ToList();
            var has = models.Any(m => ModelMatches(m, CurrentModel));
            return new EngineHealth(has,
                has ? $"Ollama no ar, modelo {CurrentModel} disponível." : $"Ollama no ar, mas o modelo {CurrentModel} não está instalado. Rode: ollama pull {CurrentModel}",
                models);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return new EngineHealth(false, $"Ollama não respondeu em {BaseUrl}. Abra o Ollama ou rode 'ollama serve'.");
        }
    }

    public async IAsyncEnumerable<ChatEvent> StreamChatAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var s = _settings.Current.Ollama;
        var body = new JsonObject
        {
            ["model"] = s.Model,
            ["messages"] = BuildMessages(request),
            ["stream"] = true,
            ["keep_alive"] = s.KeepAlive,
            ["options"] = new JsonObject
            {
                ["temperature"] = request.Temperature,
                ["num_ctx"] = s.ContextLength,
                ["num_predict"] = request.MaxTokens,
            },
        };
        if (request.Tools.Count > 0) body["tools"] = BuildTools(request.Tools);
        // Modelos com raciocínio (qwen3, deepseek-r1...) ficam lentos demais para voz; desliga o "pensar".
        if (IsThinkingModel(s.Model)) body["think"] = false;

        using var client = _http.CreateClient("llm");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/chat")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Ollama {(int)response.StatusCode}: {ExtractError(err)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var calls = new List<ToolCall>();
        int promptTokens = 0, completionTokens = 0;
        // Modelos pequenos às vezes escrevem a chamada de ferramenta como texto JSON em vez de usar tool_calls.
        // Enquanto a resposta começar com '{', seguramos o texto para não ler JSON em voz alta.
        var heldBack = new StringBuilder();
        var holding = request.Tools.Count > 0;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var e)) throw new HttpRequestException($"Ollama: {e.GetString()}");

            if (root.TryGetProperty("message", out var msg))
            {
                if (msg.TryGetProperty("content", out var c) && c.GetString() is { Length: > 0 } text)
                {
                    if (holding)
                    {
                        heldBack.Append(text);
                        var trimmed = heldBack.ToString().TrimStart();
                        if (trimmed.Length == 0) continue;
                        if (trimmed[0] is '{' or '[' || trimmed.StartsWith("<|python_tag|>")) continue;
                        holding = false;
                        yield return new TextDelta(heldBack.ToString());
                        heldBack.Clear();
                    }
                    else yield return new TextDelta(text);
                }

                if (msg.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
                {
                    foreach (var call in tc.EnumerateArray())
                    {
                        var fn = call.GetProperty("function");
                        var name = fn.GetProperty("name").GetString() ?? "";
                        var args = fn.TryGetProperty("arguments", out var a)
                            ? (a.ValueKind == JsonValueKind.String ? a.GetString() ?? "{}" : a.GetRawText())
                            : "{}";
                        var id = call.TryGetProperty("id", out var idEl) && idEl.GetString() is { Length: > 0 } cid
                            ? cid : $"call_{Guid.NewGuid():N}"[..20];
                        calls.Add(new ToolCall(id, name, args));
                    }
                }
            }

            if (root.TryGetProperty("done", out var done) && done.GetBoolean())
            {
                promptTokens = root.TryGetProperty("prompt_eval_count", out var p) ? p.GetInt32() : 0;
                completionTokens = root.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : 0;
                break;
            }
        }

        if (heldBack.Length > 0)
        {
            if (calls.Count == 0 && TryParseTextToolCall(heldBack.ToString(), request.Tools, out var recovered))
                calls.Add(recovered);
            else if (calls.Count == 0 && !LooksLikeJson(heldBack.ToString()))
                yield return new TextDelta(heldBack.ToString());
            // JSON que não é uma ferramenta conhecida é descartado (nunca é falado).
        }

        if (calls.Count > 0) yield return new ToolCallsRequested(calls);
        yield return new ChatCompleted(promptTokens, completionTokens);
    }

    /// <summary>Recupera chamadas no formato {"name": "...", "parameters"|"arguments": {...}} escritas como texto.</summary>
    internal static bool TryParseTextToolCall(string text, IReadOnlyList<ToolSpec> tools, out ToolCall call)
    {
        call = null!;
        var json = text.Trim().Replace("<|python_tag|>", "").Trim();
        if (json.StartsWith('[')) json = json.TrimStart('[').TrimEnd(']').Trim();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("name", out var n)) return false;
            var name = n.GetString() ?? "";
            var known = tools.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (known == null) return false;
            var args = root.TryGetProperty("parameters", out var p) ? p.GetRawText()
                : root.TryGetProperty("arguments", out var a) ? (a.ValueKind == JsonValueKind.String ? a.GetString() ?? "{}" : a.GetRawText())
                : "{}";
            call = new ToolCall($"call_{Guid.NewGuid():N}"[..20], known.Name, args);
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool LooksLikeJson(string text)
    {
        var t = text.Trim();
        return (t.StartsWith('{') && t.EndsWith('}')) || (t.StartsWith('[') && t.EndsWith(']')) || t.StartsWith("<|python_tag|>");
    }

    /// <summary>Gera embeddings com o modelo local (nomic-embed-text por padrão).</summary>
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        using var client = _http.CreateClient("llm");
        // num_gpu = 0: embeddings rodam na CPU (rápido para textos curtos) e não expulsam o modelo de chat da GPU.
        var res = await client.PostAsJsonAsync($"{BaseUrl}/api/embed",
            new { model = _settings.Current.Ollama.EmbeddingModel, input = text, keep_alive = "30m", options = new { num_gpu = 0 } }, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.GetProperty("embeddings")[0].EnumerateArray().Select(v => v.GetSingle()).ToArray();
    }

    private static JsonArray BuildMessages(ChatRequest request)
    {
        var arr = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt } };
        foreach (var m in request.Messages)
        {
            var o = new JsonObject { ["role"] = m.Role, ["content"] = m.Content };
            if (m.ToolCalls is { Count: > 0 })
            {
                o["tool_calls"] = new JsonArray(m.ToolCalls.Select(c => (JsonNode)new JsonObject
                {
                    ["function"] = new JsonObject
                    {
                        ["name"] = c.Name,
                        ["arguments"] = ParseObject(c.ArgumentsJson),
                    }
                }).ToArray());
            }
            if (m.Role == Roles.Tool && m.ToolName != null) o["tool_name"] = m.ToolName;
            arr.Add(o);
        }
        return arr;
    }

    private static JsonArray BuildTools(IReadOnlyList<ToolSpec> tools) =>
        new(tools.Select(t => (JsonNode)new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["parameters"] = JsonNode.Parse(t.ParametersSchema.GetRawText()),
            }
        }).ToArray());

    private static JsonNode ParseObject(string json)
    {
        try { return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) ?? new JsonObject(); }
        catch (JsonException) { return new JsonObject(); }
    }

    private static bool ModelMatches(string installed, string wanted) =>
        string.Equals(installed, wanted, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(installed, wanted + ":latest", StringComparison.OrdinalIgnoreCase);

    private static bool IsThinkingModel(string model)
    {
        var m = model.ToLowerInvariant();
        return m.StartsWith("qwen3") || m.StartsWith("deepseek-r1") || m.StartsWith("magistral");
    }

    private static string ExtractError(string body)
    {
        try { return JsonDocument.Parse(body).RootElement.GetProperty("error").GetString() ?? body; }
        catch { return body.Length > 200 ? body[..200] : body; }
    }
}
