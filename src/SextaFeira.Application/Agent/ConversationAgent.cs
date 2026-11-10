using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Agent;

namespace SextaFeira.Application.Agent;

// Eventos que o agente emite durante um turno (consumidos pelo hub SignalR).
public abstract record AgentEvent;
public sealed record AgentStatus(string State, string Detail) : AgentEvent;          // THINKING, EXECUTING
public sealed record AgentTextDelta(string Text) : AgentEvent;
public sealed record AgentSentence(string Text) : AgentEvent;                        // frase pronta para TTS
public sealed record AgentToolStarted(string Name, string Arguments) : AgentEvent;
public sealed record AgentToolFinished(string Name, bool Success, string Preview, double ElapsedMs) : AgentEvent;
public sealed record AgentTurnCompleted(string Text, string Engine, string Model, double TimeToFirstTokenMs, double TotalMs) : AgentEvent;

/// <summary>Histórico de uma conversa (compartilhado por todos os dispositivos do usuário).</summary>
public sealed class Conversation
{
    private readonly List<AgentMessage> _messages = [];
    private readonly object _lock = new();

    public string Id { get; }
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

    public Conversation(string id) => Id = id;

    public void Add(AgentMessage message)
    {
        lock (_lock) { _messages.Add(message); LastActivityUtc = DateTime.UtcNow; }
    }

    public IReadOnlyList<AgentMessage> Snapshot(int maxMessages)
    {
        lock (_lock)
        {
            if (_messages.Count <= maxMessages) return _messages.ToList();
            // Corta no início de uma mensagem do usuário para nunca deixar um resultado de ferramenta órfão.
            var start = _messages.Count - maxMessages;
            while (start < _messages.Count && _messages[start].Role != Roles.User) start++;
            return _messages.Skip(start).ToList();
        }
    }

    public void Clear() { lock (_lock) _messages.Clear(); }
}

public sealed class ConversationAgent
{
    private const int MaxToolRounds = 6;
    private const int MaxHistoryMessages = 30;
    private const int MaxToolOutputChars = 6000;

    private readonly EngineRouter _router;
    private readonly ToolRegistry _tools;
    private readonly IMemoryStore _memory;
    private readonly ISettingsStore _settings;
    private readonly ILogger<ConversationAgent> _logger;

    public ConversationAgent(EngineRouter router, ToolRegistry tools, IMemoryStore memory, ISettingsStore settings, ILogger<ConversationAgent> logger)
    {
        _router = router;
        _tools = tools;
        _memory = memory;
        _settings = settings;
        _logger = logger;
    }

    public async IAsyncEnumerable<AgentEvent> RunTurnAsync(
        Conversation conversation,
        string userText,
        ToolContext context,
        string? deviceName = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var settings = _settings.Current;
        var total = Stopwatch.StartNew();
        double ttft = -1;

        conversation.Add(AgentMessage.User(userText));
        yield return new AgentStatus("THINKING", "Pensando...");

        var memories = await RecallAsync(userText, ct);
        var tools = _tools.GetAll();
        var systemPrompt = SystemPromptBuilder.BuildStatic(settings, tools);
        var contextNote = SystemPromptBuilder.BuildContextNote(settings, memories, deviceName);
        IReadOnlyList<ToolSpec> toolSpecs = tools.Select(t => t.Spec).ToList();

        var engine = _router.ResolveActive();
        var usedFallback = false;
        var retriedWithoutTools = false;
        var spokenText = new StringBuilder();

        for (var round = 0; round <= MaxToolRounds; round++)
        {
            var request = new ChatRequest(systemPrompt, WithContextNote(conversation.Snapshot(MaxHistoryMessages), contextNote),
                round < MaxToolRounds ? toolSpecs : []);
            var splitter = new SentenceSplitter();
            var roundText = new StringBuilder();
            List<ToolCall>? toolCalls = null;
            EngineStateCaptured? engineState = null;
            Exception? failure = null;

            await using (var enumerator = engine.StreamChatAsync(request, ct).GetAsyncEnumerator(ct))
            {
                while (true)
                {
                    ChatEvent evt;
                    try
                    {
                        if (!await enumerator.MoveNextAsync()) break;
                        evt = enumerator.Current;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex) { failure = ex; break; }

                    switch (evt)
                    {
                        case TextDelta d when d.Text.Length > 0:
                            if (ttft < 0) ttft = total.Elapsed.TotalMilliseconds;
                            roundText.Append(d.Text);
                            yield return new AgentTextDelta(d.Text);
                            foreach (var sentence in splitter.Push(d.Text))
                            {
                                var clean = SpeechText.Clean(sentence);
                                if (clean.Length > 0) yield return new AgentSentence(clean);
                            }
                            break;
                        case ToolCallsRequested t:
                            toolCalls = [.. toolCalls ?? [], .. t.Calls];
                            break;
                        case EngineStateCaptured s:
                            engineState = s;
                            break;
                    }
                }
            }

            if (failure != null)
            {
                _logger.LogWarning(failure, "Engine {Engine} falhou.", engine.Id);
                // Fallback para o modelo local apenas se nada foi dito ainda neste turno.
                if (!engine.IsLocal && settings.FallbackToLocal && !usedFallback && roundText.Length == 0 && spokenText.Length == 0)
                {
                    usedFallback = true;
                    engine = _router.Local;
                    yield return new AgentStatus("THINKING", $"{failure.Message} — usando o modelo local.");
                    round--; // repete a rodada com a engine local
                    continue;
                }

                var msg = roundText.Length == 0
                    ? $"Desculpa, não consegui falar com o modelo {engine.DisplayName}. {ShortError(failure)}"
                    : "";
                if (msg.Length > 0)
                {
                    roundText.Append(msg);
                    yield return new AgentTextDelta(msg);
                    yield return new AgentSentence(msg);
                }
            }

            var rest = splitter.Flush();
            if (rest != null)
            {
                var clean = SpeechText.Clean(rest);
                if (clean.Length > 0) yield return new AgentSentence(clean);
            }

            spokenText.Append(roundText);

            // Rodada vazia (ex.: o modelo tentou uma ferramenta que não existe): tenta de novo, só conversando.
            if (failure == null && roundText.Length == 0 && toolCalls is not { Count: > 0 } && !retriedWithoutTools)
            {
                retriedWithoutTools = true;
                toolSpecs = [];
                round--;
                continue;
            }

            var finished = toolCalls is not { Count: > 0 } || failure != null;
            conversation.Add(AgentMessage.Assistant(roundText.ToString(), finished ? null : toolCalls) with
            {
                EngineState = engineState?.State,
                EngineId = engineState?.EngineId
            });
            if (finished) break;
            if (spokenText.Length > 0 && !char.IsWhiteSpace(spokenText[^1])) spokenText.Append(' ');

            foreach (var call in toolCalls)
            {
                yield return new AgentStatus("EXECUTING", $"Usando {call.Name}...");
                yield return new AgentToolStarted(call.Name, call.ArgumentsJson);
                var sw = Stopwatch.StartNew();
                var (ok, output) = await ExecuteToolAsync(call, context, ct);
                sw.Stop();
                conversation.Add(AgentMessage.ToolResult(call, output));
                yield return new AgentToolFinished(call.Name, ok, Preview(output), sw.Elapsed.TotalMilliseconds);
            }

            yield return new AgentStatus("THINKING", "Analisando resultado...");
        }

        total.Stop();
        yield return new AgentTurnCompleted(spokenText.ToString().Trim(), engine.Id, engine.CurrentModel, Math.Round(ttft, 0), Math.Round(total.Elapsed.TotalMilliseconds, 0));
    }

    private async Task<(bool Ok, string Output)> ExecuteToolAsync(ToolCall call, ToolContext context, CancellationToken ct)
    {
        var tool = _tools.Find(call.Name);
        if (tool == null)
            return (false, $"Erro: a ferramenta '{call.Name}' não existe. Ferramentas disponíveis: {string.Join(", ", _tools.GetAll().Select(t => t.Spec.Name))}.");

        JsonElement args;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
            args = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return (false, "Erro: os argumentos não são um JSON válido.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // Builds de autoprogramação podem levar alguns minutos; o resto tem 90 s.
        timeout.CancelAfter(tool.Source == "code" ? TimeSpan.FromMinutes(6) : TimeSpan.FromSeconds(90));
        try
        {
            var output = await tool.InvokeAsync(args, context, timeout.Token);
            if (output.Length > MaxToolOutputChars) output = output[..MaxToolOutputChars] + "\n…(saída truncada)";
            return (true, output);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (false, "Erro: a ferramenta demorou demais e foi cancelada.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Ferramenta {Tool} falhou", call.Name);
            return (false, $"Erro ao executar {call.Name}: {ex.Message}");
        }
    }

    private async Task<IReadOnlyList<MemoryItem>> RecallAsync(string query, CancellationToken ct)
    {
        try { return await _memory.SearchAsync(query, 4, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Memória indisponível");
            return [];
        }
    }

    /// <summary>Anexa a nota de contexto (hora, memórias) só à última fala do usuário, sem alterar o histórico salvo.</summary>
    private static IReadOnlyList<AgentMessage> WithContextNote(IReadOnlyList<AgentMessage> messages, string note)
    {
        var list = messages.ToList();
        var idx = list.FindLastIndex(m => m.Role == Roles.User);
        if (idx >= 0) list[idx] = list[idx] with { Content = $"{note}\n{list[idx].Content}" };
        return list;
    }

    private static string Preview(string s) => s.Length <= 160 ? s : s[..160] + "…";

    private static string ShortError(Exception ex) =>
        ex is HttpRequestException ? "O serviço não respondeu." : ex.Message.Length > 120 ? ex.Message[..120] : ex.Message;
}
