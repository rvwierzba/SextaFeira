using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SextaFeira.Application.Agent;
using SextaFeira.Domain.Agent;
using SextaFeira.Infrastructure.Engines;
using Xunit;

namespace SextaFeira.Tests;

public class SentenceSplitterTests
{
    [Fact]
    public void Emite_frases_assim_que_terminam()
    {
        var s = new SentenceSplitter();
        var out1 = s.Push("Oi, Rafael. Tudo ").ToList();
        Assert.Equal(["Oi, Rafael."], out1);
        var out2 = s.Push("certo por aqui! E com você?").ToList();
        Assert.Equal(["Tudo certo por aqui!"], out2);
        Assert.Equal("E com você?", s.Flush());
    }

    [Fact]
    public void Nao_corta_numeros_decimais_nem_abreviacoes()
    {
        var s = new SentenceSplitter();
        var sentences = s.Push("A versão 3.5 saiu ontem. Falei com o Dr. Silva hoje. ").ToList();
        Assert.Equal(["A versão 3.5 saiu ontem.", "Falei com o Dr. Silva hoje."], sentences);
    }

    [Fact]
    public void Limpa_markdown_para_fala()
    {
        Assert.Equal("Importante: compre leite", SpeechText.Clean("**Importante:** compre leite"));
        Assert.Equal("Veja o link", SpeechText.Clean("Veja https://exemplo.com"));
    }
}

public class WakeWordTests
{
    private static readonly string[] Words = ["sexta-feira", "sexta feira", "sexta", "friday"];

    [Theory]
    [InlineData("Sexta-feira, que horas são?", "que horas são?")]
    [InlineData("Ei Sexta, toca uma música", "toca uma música")]
    [InlineData("ok sexta feira qual a previsão", "qual a previsão")]
    [InlineData("Hey Friday, what's up", "what's up")]
    [InlineData("Sesta-Feira, qual é a capital do Japão?", "qual é a capital do Japão?")]
    [InlineData("Cesta feira, toca música", "toca música")]
    public void Detecta_e_remove_palavra_de_ativacao(string input, string expected)
    {
        Assert.True(WakeWord.TryStrip(input, Words, out var command));
        Assert.Equal(expected, command);
    }

    [Theory]
    [InlineData("hoje é sexta, finalmente")]
    [InlineData("vamos sair na sexta-feira à noite")]
    [InlineData("que horas são?")]
    [InlineData("festa hoje à noite?")]
    [InlineData("testa esse código pra mim")]
    public void Nao_ativa_quando_sexta_e_so_um_dia_da_semana(string input)
    {
        Assert.False(WakeWord.TryStrip(input, Words, out _));
    }

    [Fact]
    public void So_o_nome_retorna_comando_vazio()
    {
        Assert.True(WakeWord.TryStrip("Sexta-feira!", Words, out var command));
        Assert.Equal("", command);
    }
}

public class OllamaTextToolCallTests
{
    private static readonly IReadOnlyList<ToolSpec> Tools =
        [new ToolSpec("get_weather", "clima", JsonDocument.Parse("""{"type":"object","properties":{"city":{"type":"string"}}}""").RootElement)];

    [Fact]
    public void Recupera_chamada_escrita_como_texto()
    {
        Assert.True(OllamaEngine.TryParseTextToolCall("""{"name": "get_weather", "parameters": {"city": "Recife"}}""", Tools, out var call));
        Assert.Equal("get_weather", call.Name);
        Assert.Contains("Recife", call.ArgumentsJson);
    }

    [Fact]
    public void Ignora_ferramenta_inexistente()
    {
        Assert.False(OllamaEngine.TryParseTextToolCall("""{"name": "get_time", "parameters": {}}""", Tools, out _));
    }
}

public class ConversationAgentTests
{
    /// <summary>Engine falsa que segue um roteiro de respostas.</summary>
    private sealed class ScriptedEngine(params Func<ChatRequest, IEnumerable<ChatEvent>>[] script) : IChatEngine
    {
        private int _call;
        public List<ChatRequest> Requests { get; } = [];
        public string Id => "fake";
        public string DisplayName => "Fake";
        public bool IsLocal => true;
        public string CurrentModel => "fake-1";
        public Task<EngineHealth> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(new EngineHealth(true, "ok"));

        public async IAsyncEnumerable<ChatEvent> StreamChatAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
        {
            Requests.Add(request);
            foreach (var e in script[Math.Min(_call++, script.Length - 1)](request))
            {
                await Task.Yield();
                yield return e;
            }
        }
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public AssistantSettings Current { get; } = new() { ActiveEngine = "fake", UserName = "Rafael" };
        public Task SaveAsync(AssistantSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public event Action<AssistantSettings>? Changed { add { } remove { } }
    }

    private sealed class NoMemory : IMemoryStore
    {
        public Task<MemoryItem> SaveAsync(string text, CancellationToken ct = default) => Task.FromResult(new MemoryItem("1", text, DateTime.UtcNow));
        public Task<IReadOnlyList<MemoryItem>> SearchAsync(string query, int limit = 4, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MemoryItem>>([]);
        public Task<IReadOnlyList<MemoryItem>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MemoryItem>>([]);
        public Task<bool> DeleteAsync(string id, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class EchoTool : ITool
    {
        public int Calls;
        public ToolSpec Spec { get; } = new("echo", "repete", JsonDocument.Parse("""{"type":"object","properties":{"text":{"type":"string"}}}""").RootElement);
        public string Source => "builtin";
        public Task<string> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult("eco: " + arguments.GetProperty("text").GetString());
        }
    }

    private static ConversationAgent CreateAgent(IChatEngine engine, ITool tool)
    {
        var settings = new FakeSettings();
        return new ConversationAgent(new EngineRouter([engine], settings), new ToolRegistry([tool], [], settings),
            new NoMemory(), settings, NullLogger<ConversationAgent>.Instance);
    }

    [Fact]
    public async Task Executa_ferramenta_e_devolve_resultado_ao_modelo()
    {
        var engine = new ScriptedEngine(
            _ => [new TextDelta("Deixa eu ver. "), new ToolCallsRequested([new ToolCall("c1", "echo", """{"text":"oi"}""")])],
            req => [new TextDelta(req.Messages.Last().Content.Contains("eco: oi") ? "O resultado foi oi." : "Não vi o resultado.")]);
        var tool = new EchoTool();
        var conversation = new Conversation("t");

        var events = new List<AgentEvent>();
        await foreach (var e in CreateAgent(engine, tool).RunTurnAsync(conversation, "testa", new ToolContext("t", null)))
            events.Add(e);

        Assert.Equal(1, tool.Calls);
        var done = events.OfType<AgentTurnCompleted>().Single();
        Assert.Equal("Deixa eu ver. O resultado foi oi.", done.Text);
        Assert.Equal(["Deixa eu ver.", "O resultado foi oi."], events.OfType<AgentSentence>().Select(s => s.Text));
        // histórico: user, assistant(tool call), tool, assistant
        Assert.Equal([Roles.User, Roles.Assistant, Roles.Tool, Roles.Assistant], conversation.Snapshot(50).Select(m => m.Role));
    }

    [Fact]
    public async Task Mantem_contexto_entre_turnos_e_nota_de_contexto_so_na_ultima_fala()
    {
        var engine = new ScriptedEngine(_ => [new TextDelta("Certo.")]);
        var agent = CreateAgent(engine, new EchoTool());
        var conversation = new Conversation("t");

        await foreach (var _ in agent.RunTurnAsync(conversation, "primeira", new ToolContext("t", null))) { }
        await foreach (var _ in agent.RunTurnAsync(conversation, "segunda", new ToolContext("t", null))) { }

        var last = engine.Requests.Last();
        Assert.Equal(3, last.Messages.Count); // primeira, Certo., segunda
        Assert.Equal("primeira", last.Messages[0].Content); // histórico sem nota
        Assert.StartsWith("[contexto:", last.Messages[2].Content);
        Assert.EndsWith("segunda", last.Messages[2].Content);
        // O prompt de sistema é idêntico entre turnos (cache do modelo).
        Assert.Equal(engine.Requests[0].SystemPrompt, last.SystemPrompt);
    }

    [Fact]
    public async Task Rodada_vazia_tenta_de_novo_sem_ferramentas()
    {
        var engine = new ScriptedEngine(_ => [], req => [new TextDelta(req.Tools.Count == 0 ? "Respondi." : "")]);
        var events = new List<AgentEvent>();
        await foreach (var e in CreateAgent(engine, new EchoTool()).RunTurnAsync(new Conversation("t"), "oi", new ToolContext("t", null)))
            events.Add(e);
        Assert.Equal("Respondi.", events.OfType<AgentTurnCompleted>().Single().Text);
    }
}
