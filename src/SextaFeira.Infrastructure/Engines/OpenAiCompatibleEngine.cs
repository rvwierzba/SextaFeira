using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SextaFeira.Domain.Agent;

namespace SextaFeira.Infrastructure.Engines;

/// <summary>
/// Engine para qualquer API compatível com OpenAI Chat Completions (streaming SSE + tool calling).
/// Usada para o ChatGPT (OpenAI) e para o Gemini (endpoint OpenAI-compatível do Google).
/// </summary>
public abstract class OpenAiCompatibleEngine : IChatEngine
{
    private readonly IHttpClientFactory _http;
    protected readonly ISettingsStore Settings;

    protected OpenAiCompatibleEngine(IHttpClientFactory http, ISettingsStore settings)
    {
        _http = http;
        Settings = settings;
    }

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public bool IsLocal => false;
    public string CurrentModel => Config.Model;

    protected abstract CloudEngineSettings Config { get; }
    protected abstract string? EnvironmentApiKey { get; }
    protected virtual bool SupportsStreamUsage => true;
    protected virtual string MaxTokensField => "max_tokens";

    protected string? ApiKey => string.IsNullOrWhiteSpace(Config.ApiKey) ? EnvironmentApiKey : Config.ApiKey;

    public Task<EngineHealth> CheckHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(string.IsNullOrWhiteSpace(ApiKey)
            ? new EngineHealth(false, $"Sem chave do {DisplayName}. Cole a chave nas configurações.")
            : new EngineHealth(true, $"{DisplayName} pronto ({CurrentModel})."));

    public async IAsyncEnumerable<ChatEvent> StreamChatAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = ApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException($"Chave do {DisplayName} não configurada.");

        var body = new JsonObject
        {
            ["model"] = Config.Model,
            ["messages"] = BuildMessages(request),
            ["stream"] = true,
            [MaxTokensField] = request.MaxTokens,
        };
        if (SupportsTemperature(Config.Model)) body["temperature"] = request.Temperature;
        if (SupportsStreamUsage) body["stream_options"] = new JsonObject { ["include_usage"] = true };
        if (request.Tools.Count > 0) body["tools"] = BuildTools(request.Tools);

        using var client = _http.CreateClient("llm");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{Config.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"{DisplayName} {(int)response.StatusCode}: {Truncate(err)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var partial = new SortedDictionary<int, (string Id, string Name, StringBuilder Args)>();
        int promptTokens = 0, completionTokens = 0;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:")) continue;
            var data = line[5..].Trim();
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;

            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                promptTokens = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : promptTokens;
                completionTokens = usage.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : completionTokens;
            }
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) continue;

            var delta = choices[0].GetProperty("delta");
            if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String && content.GetString() is { Length: > 0 } text)
                yield return new TextDelta(text);

            if (delta.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in tcs.EnumerateArray())
                {
                    var index = tc.TryGetProperty("index", out var i) ? i.GetInt32() : partial.Count;
                    if (!partial.TryGetValue(index, out var entry))
                        entry = ("", "", new StringBuilder());
                    if (tc.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } idStr) entry.Id = idStr;
                    if (tc.TryGetProperty("function", out var fn))
                    {
                        if (fn.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name) entry.Name = name;
                        if (fn.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.String) entry.Args.Append(a.GetString());
                    }
                    partial[index] = entry;
                }
            }
        }

        if (partial.Count > 0)
        {
            yield return new ToolCallsRequested(partial.Values
                .Where(p => p.Name.Length > 0)
                .Select(p => new ToolCall(p.Id.Length > 0 ? p.Id : $"call_{Guid.NewGuid():N}"[..20], p.Name, p.Args.Length > 0 ? p.Args.ToString() : "{}"))
                .ToList());
        }
        yield return new ChatCompleted(promptTokens, completionTokens);
    }

    private static JsonArray BuildMessages(ChatRequest request)
    {
        var arr = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt } };
        foreach (var m in request.Messages)
        {
            switch (m.Role)
            {
                case Roles.Tool:
                    arr.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = m.ToolCallId, ["content"] = m.Content });
                    break;
                case Roles.Assistant when m.ToolCalls is { Count: > 0 }:
                    arr.Add(new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = m.Content.Length > 0 ? m.Content : null,
                        ["tool_calls"] = new JsonArray(m.ToolCalls.Select(c => (JsonNode)new JsonObject
                        {
                            ["id"] = c.Id,
                            ["type"] = "function",
                            ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.ArgumentsJson },
                        }).ToArray()),
                    });
                    break;
                default:
                    arr.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
                    break;
            }
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

    /// <summary>Modelos de raciocínio da OpenAI (o1, o3, o4, gpt-5...) só aceitam a temperatura padrão.</summary>
    protected virtual bool SupportsTemperature(string model)
    {
        var m = model.ToLowerInvariant();
        return !(m.StartsWith("o1") || m.StartsWith("o3") || m.StartsWith("o4") || m.StartsWith("gpt-5"));
    }

    private static string Truncate(string s) => s.Length > 300 ? s[..300] : s;
}

public sealed class OpenAiEngine : OpenAiCompatibleEngine
{
    public OpenAiEngine(IHttpClientFactory http, ISettingsStore settings) : base(http, settings) { }
    public override string Id => "openai";
    public override string DisplayName => "ChatGPT (OpenAI)";
    protected override CloudEngineSettings Config => Settings.Current.OpenAI;
    protected override string? EnvironmentApiKey => Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    protected override string MaxTokensField => "max_completion_tokens";
}

public sealed class GeminiEngine : OpenAiCompatibleEngine
{
    public GeminiEngine(IHttpClientFactory http, ISettingsStore settings) : base(http, settings) { }
    public override string Id => "gemini";
    public override string DisplayName => "Gemini (Google)";
    protected override CloudEngineSettings Config => Settings.Current.Gemini;
    protected override string? EnvironmentApiKey =>
        Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
    protected override bool SupportsStreamUsage => false;
    protected override bool SupportsTemperature(string model) => true;
}
