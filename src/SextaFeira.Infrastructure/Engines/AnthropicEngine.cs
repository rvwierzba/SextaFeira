using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using SextaFeira.Domain.Agent;

namespace SextaFeira.Infrastructure.Engines;

/// <summary>
/// Engine Claude (Anthropic) usando o SDK oficial em C#, com streaming e tool use.
/// Sem chave nas configurações, o SDK usa as credenciais do ambiente (ANTHROPIC_API_KEY ou perfil do `ant auth login`).
/// </summary>
public sealed class AnthropicEngine : IChatEngine
{
    private const string FallbackBeta = "server-side-fallback-2026-07-01";
    private readonly ISettingsStore _settings;
    private AnthropicClient? _client;
    private string? _clientKey;

    public AnthropicEngine(ISettingsStore settings) => _settings = settings;

    public string Id => "anthropic";
    public string DisplayName => "Claude (Anthropic)";
    public bool IsLocal => false;
    public string CurrentModel => _settings.Current.Anthropic.Model;

    private string ConfiguredKey => _settings.Current.Anthropic.ApiKey.Trim();

    public Task<EngineHealth> CheckHealthAsync(CancellationToken ct = default)
    {
        if (ConfiguredKey.Length > 0 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
            return Task.FromResult(new EngineHealth(true, $"Claude pronto ({CurrentModel})."));
        return Task.FromResult(new EngineHealth(true,
            "Sem chave salva: vou usar as credenciais do ambiente (perfil do 'ant auth login'). Se falhar, cole uma chave nas configurações."));
    }

    private AnthropicClient Client()
    {
        var key = ConfiguredKey;
        if (_client == null || _clientKey != key)
        {
            _client = key.Length > 0 ? new AnthropicClient { ApiKey = key } : new AnthropicClient();
            _clientKey = key;
        }
        return _client;
    }

    public async IAsyncEnumerable<ChatEvent> StreamChatAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var model = CurrentModel;
        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = Math.Max(request.MaxTokens, 4096),
            System = new List<BetaTextBlockParam>
            {
                new() { Text = request.SystemPrompt, CacheControl = new BetaCacheControlEphemeral() }
            },
            Messages = BuildMessages(request.Messages),
            Tools = request.Tools.Select(ToTool).ToList(),
        };
        if (IsClaude5Family(model))
        {
            // Conversa por voz: esforço baixo = resposta rápida e direta.
            parameters = parameters with { OutputConfig = new BetaOutputConfig { Effort = Effort.Low } };
        }
        if (SupportsServerFallback(model))
        {
            parameters = parameters with { Betas = [FallbackBeta], Fallbacks = new Default() };
        }

        // Montagem dos blocos do assistente desta rodada (para devolver intactos no próximo pedido).
        var assistantBlocks = new List<BetaContentBlockParam>();
        var calls = new List<ToolCall>();
        var currentText = new StringBuilder();
        var currentThinking = new StringBuilder();
        string? currentSignature = null;
        string? currentToolId = null, currentToolName = null;
        var currentToolJson = new StringBuilder();
        string? currentKind = null;
        string? stopReason = null;
        long inputTokens = 0, outputTokens = 0;

        await foreach (var ev in Client().Beta.Messages.CreateStreaming(parameters, ct))
        {
            if (ev.TryPickStart(out var start))
            {
                inputTokens = start.Message.Usage.InputTokens;
            }
            else if (ev.TryPickContentBlockStart(out var blockStart))
            {
                currentText.Clear(); currentThinking.Clear(); currentToolJson.Clear();
                currentSignature = null; currentToolId = null; currentToolName = null; currentKind = null;

                if (blockStart.ContentBlock.TryPickBetaText(out _)) currentKind = "text";
                else if (blockStart.ContentBlock.TryPickBetaThinking(out var th)) { currentKind = "thinking"; currentSignature = th.Signature; }
                else if (blockStart.ContentBlock.TryPickBetaRedactedThinking(out var rt))
                {
                    currentKind = "redacted";
                    assistantBlocks.Add(new BetaRedactedThinkingBlockParam { Data = rt.Data });
                }
                else if (blockStart.ContentBlock.TryPickBetaToolUse(out var tu))
                {
                    currentKind = "tool"; currentToolId = tu.ID; currentToolName = tu.Name;
                }
            }
            else if (ev.TryPickContentBlockDelta(out var blockDelta))
            {
                if (blockDelta.Delta.TryPickText(out var t))
                {
                    currentText.Append(t.Text);
                    yield return new TextDelta(t.Text);
                }
                else if (blockDelta.Delta.TryPickInputJson(out var j)) currentToolJson.Append(j.PartialJson);
                else if (blockDelta.Delta.TryPickThinking(out var th)) currentThinking.Append(th.Thinking);
                else if (blockDelta.Delta.TryPickSignature(out var sig)) currentSignature = sig.Signature;
            }
            else if (ev.TryPickContentBlockStop(out _))
            {
                switch (currentKind)
                {
                    case "text" when currentText.Length > 0:
                        assistantBlocks.Add(new BetaTextBlockParam { Text = currentText.ToString() });
                        break;
                    case "thinking":
                        assistantBlocks.Add(new BetaThinkingBlockParam { Thinking = currentThinking.ToString(), Signature = currentSignature ?? "" });
                        break;
                    case "tool" when currentToolId != null && currentToolName != null:
                        var json = currentToolJson.Length > 0 ? currentToolJson.ToString() : "{}";
                        calls.Add(new ToolCall(currentToolId, currentToolName, json));
                        assistantBlocks.Add(new BetaToolUseBlockParam { ID = currentToolId, Name = currentToolName, Input = ParseInput(json) });
                        break;
                }
                currentKind = null;
            }
            else if (ev.TryPickDelta(out var messageDelta))
            {
                stopReason = messageDelta.Delta.StopReason?.ToString();
                outputTokens = messageDelta.Usage.OutputTokens;
            }
        }

        if (string.Equals(stopReason, "refusal", StringComparison.OrdinalIgnoreCase) && calls.Count == 0)
            yield return new TextDelta("Prefiro não seguir com esse pedido.");

        yield return new EngineStateCaptured(Id, assistantBlocks);
        if (calls.Count > 0 && !string.Equals(stopReason, "max_tokens", StringComparison.OrdinalIgnoreCase))
            yield return new ToolCallsRequested(calls);
        yield return new ChatCompleted((int)inputTokens, (int)outputTokens);
    }

    private List<BetaMessageParam> BuildMessages(IReadOnlyList<AgentMessage> messages)
    {
        var result = new List<BetaMessageParam>();
        var pendingResults = new List<BetaContentBlockParam>();

        void FlushResults()
        {
            if (pendingResults.Count == 0) return;
            result.Add(new BetaUserMessageParam { Content = pendingResults.ToList() });
            pendingResults.Clear();
        }

        foreach (var m in messages)
        {
            if (m.Role == Roles.Tool)
            {
                pendingResults.Add(new BetaToolResultBlockParam
                {
                    ToolUseID = m.ToolCallId ?? "",
                    Content = m.Content.Length > 0 ? m.Content : "(vazio)",
                    IsError = m.Content.StartsWith("Erro", StringComparison.Ordinal),
                });
                continue;
            }

            FlushResults();
            if (m.Role == Roles.User)
            {
                result.Add(new BetaUserMessageParam { Content = m.Content });
            }
            else if (m.Role == Roles.Assistant)
            {
                List<BetaContentBlockParam> blocks;
                if (m.EngineId == Id && m.EngineState is List<BetaContentBlockParam> saved)
                {
                    blocks = saved;
                }
                else
                {
                    blocks = [];
                    if (m.Content.Length > 0) blocks.Add(new BetaTextBlockParam { Text = m.Content });
                    foreach (var c in m.ToolCalls ?? [])
                        blocks.Add(new BetaToolUseBlockParam { ID = c.Id, Name = c.Name, Input = ParseInput(c.ArgumentsJson) });
                }
                if (blocks.Count > 0) result.Add(new BetaAssistantMessageParam { Content = blocks });
            }
        }
        FlushResults();
        return result;
    }

    private static BetaToolUnion ToTool(ToolSpec spec)
    {
        var schema = spec.ParametersSchema;
        var properties = new Dictionary<string, JsonElement>();
        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            foreach (var p in props.EnumerateObject()) properties[p.Name] = p.Value.Clone();

        var required = new List<string>();
        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array)
            required.AddRange(req.EnumerateArray().Select(r => r.GetString() ?? "").Where(r => r.Length > 0));

        return new BetaTool
        {
            Name = spec.Name,
            Description = spec.Description,
            InputSchema = new() { Properties = properties, Required = required },
            EagerInputStreaming = true,
        };
    }

    private static Dictionary<string, JsonElement> ParseInput(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
                : [];
        }
        catch (JsonException) { return []; }
    }

    private static bool IsClaude5Family(string model) =>
        model.StartsWith("claude-opus-5") || model.StartsWith("claude-sonnet-5") ||
        model.StartsWith("claude-haiku-5") || model.StartsWith("claude-fable-5");

    private static bool SupportsServerFallback(string model) =>
        model is "claude-opus-5-5" or "claude-opus-5" or "claude-sonnet-5-5" or "claude-fable-5-1" or "claude-fable-5-2";
}
