using System.Text.Json;

namespace SextaFeira.Domain.Agent;

// ─────────────────────────────────────────────────────────────────────────────
// Mensagens de conversa (formato neutro, convertido por cada engine)
// ─────────────────────────────────────────────────────────────────────────────

public static class Roles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool";
}

public sealed record ToolCall(string Id, string Name, string ArgumentsJson);

public sealed record AgentMessage(
    string Role,
    string Content,
    IReadOnlyList<ToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? ToolName = null)
{
    /// <summary>
    /// Estado opaco específico da engine que gerou a mensagem (ex.: blocos de raciocínio assinados do Claude,
    /// que precisam voltar intactos no próximo pedido). Outras engines ignoram.
    /// </summary>
    public object? EngineState { get; init; }
    public string? EngineId { get; init; }

    public static AgentMessage User(string text) => new(Roles.User, text);
    public static AgentMessage Assistant(string text, IReadOnlyList<ToolCall>? calls = null) => new(Roles.Assistant, text, calls);
    public static AgentMessage ToolResult(ToolCall call, string output) => new(Roles.Tool, output, null, call.Id, call.Name);
}

/// <summary>Definição de ferramenta exposta ao modelo (JSON Schema dos parâmetros).</summary>
public sealed record ToolSpec(string Name, string Description, JsonElement ParametersSchema);

public sealed record ChatRequest(
    string SystemPrompt,
    IReadOnlyList<AgentMessage> Messages,
    IReadOnlyList<ToolSpec> Tools,
    double Temperature = 0.6,
    int MaxTokens = 1024);

// ─────────────────────────────────────────────────────────────────────────────
// Eventos de streaming emitidos por uma engine
// ─────────────────────────────────────────────────────────────────────────────

public abstract record ChatEvent;
public sealed record TextDelta(string Text) : ChatEvent;
public sealed record ToolCallsRequested(IReadOnlyList<ToolCall> Calls) : ChatEvent;
/// <summary>Estado opaco que a engine quer anexar à mensagem do assistente desta rodada.</summary>
public sealed record EngineStateCaptured(string EngineId, object State) : ChatEvent;
public sealed record ChatCompleted(int PromptTokens, int CompletionTokens) : ChatEvent;

/// <summary>
/// Um backend de inferência (Ollama local, OpenAI, Anthropic, Gemini, qualquer API compatível com OpenAI).
/// </summary>
public interface IChatEngine
{
    /// <summary>Identificador estável: "ollama", "openai", "anthropic", "gemini".</summary>
    string Id { get; }
    string DisplayName { get; }
    bool IsLocal { get; }
    string CurrentModel { get; }

    /// <summary>Verifica se a engine está pronta (serviço no ar / chave configurada).</summary>
    Task<EngineHealth> CheckHealthAsync(CancellationToken ct = default);

    IAsyncEnumerable<ChatEvent> StreamChatAsync(ChatRequest request, CancellationToken ct = default);
}

public sealed record EngineHealth(bool Available, string Detail, IReadOnlyList<string>? Models = null);

// ─────────────────────────────────────────────────────────────────────────────
// Ferramentas
// ─────────────────────────────────────────────────────────────────────────────

public sealed record ToolContext(string ConversationId, string? DeviceId);

public interface ITool
{
    ToolSpec Spec { get; }
    /// <summary>Origem da ferramenta (ex.: "builtin", "code", "mcp:spotify").</summary>
    string Source { get; }
    Task<string> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct);
}

/// <summary>Fonte dinâmica de ferramentas (ex.: servidores MCP conectados).</summary>
public interface IToolProvider
{
    IReadOnlyList<ITool> GetTools();
}

// ─────────────────────────────────────────────────────────────────────────────
// Memória de longo prazo
// ─────────────────────────────────────────────────────────────────────────────

public sealed record MemoryItem(string Id, string Text, DateTime CreatedAt, double Score = 0);

public interface IMemoryStore
{
    Task<MemoryItem> SaveAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<MemoryItem>> SearchAsync(string query, int limit = 4, CancellationToken ct = default);
    Task<IReadOnlyList<MemoryItem>> ListAsync(CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// Voz
// ─────────────────────────────────────────────────────────────────────────────

public sealed record TranscriptionResult(string Text, double NoSpeechProbability, TimeSpan Elapsed);

public interface ISpeechToText
{
    bool IsReady { get; }
    string Status { get; }
    /// <summary>Transcreve PCM 16-bit little-endian mono 16 kHz.</summary>
    Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<byte> pcm16Mono16k, CancellationToken ct = default);
}

public interface ITextToSpeech
{
    string Status { get; }
    /// <summary>Retorna áudio MP3 ou vazio se indisponível (o cliente cai para a voz do navegador).</summary>
    Task<byte[]> SynthesizeAsync(string text, CancellationToken ct = default);
}
