using System.Text.Json;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Application.Orchestrator;

public record ProcessUserInputRequest(
    string UserInput,
    Guid? SessionId = null,
    bool EnableVoiceOutput = true,
    LLMProviderType? PreferredProvider = null
);

public record ProcessUserInputResponse(
    Guid SessionId,
    string TextResponse,
    LLMProviderType ProviderUsed,
    string ModelUsed,
    AgentState FinalState,
    List<ToolExecutionResult> ExecutedTools,
    List<MemoryVector> RecalledMemories,
    byte[]? AudioResponse = null
);

public class CognitiveOrchestrator
{
    private readonly IEnumerable<ILLMProvider> _llmProviders;
    private readonly IMemoryStore _memoryStore;
    private readonly IMcpHost _mcpHost;
    private readonly IVoiceEngine _voiceEngine;
    private readonly ILogger<CognitiveOrchestrator> _logger;

    private static readonly Dictionary<Guid, ConversationSession> _sessionCache = new();

    public CognitiveOrchestrator(
        IEnumerable<ILLMProvider> llmProviders,
        IMemoryStore memoryStore,
        IMcpHost mcpHost,
        IVoiceEngine voiceEngine,
        ILogger<CognitiveOrchestrator> logger)
    {
        _llmProviders = llmProviders;
        _memoryStore = memoryStore;
        _mcpHost = mcpHost;
        _voiceEngine = voiceEngine;
        _logger = logger;
    }

    public async Task<ProcessUserInputResponse> ProcessInputAsync(
        ProcessUserInputRequest request, 
        Action<AgentState, string>? onStateChanged = null,
        CancellationToken cancellationToken = default)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid();
        if (!_sessionCache.TryGetValue(sessionId, out var session))
        {
            session = new ConversationSession { Id = sessionId };
            _sessionCache[sessionId] = session;
        }

        onStateChanged?.Invoke(AgentState.Listening, "Comando de voz/texto recebido.");

        // 1. Gravar mensagem do usuário no histórico da sessão
        var userMsg = new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = request.UserInput,
            Timestamp = DateTime.UtcNow
        };
        session.Messages.Add(userMsg);

        onStateChanged?.Invoke(AgentState.Thinking, "Buscando contexto e memórias de longo prazo...");

        // 2. Recuperação de Memória Vetorial (RAG)
        var recalledMemories = await _memoryStore.SearchSimilarAsync(request.UserInput, limit: 4, cancellationToken: cancellationToken);
        var memoryContext = string.Join("\n", recalledMemories.Select(m => $"- [{m.Category}] {m.Content}"));

        // 3. Montar System Prompt do Sexta-Feira
        var systemPrompt = $"""
            Você é a SEXTA-FEIRA (F.R.I.D.A.Y.), um Harness de Inteligência Artificial autônomo, multimodal, ultra-eficiente e leal.
            Você se comunica prioritariamente em Português do Brasil (PT-BR) com tom profissional, preciso, sofisticado e tecnológico.
            
            DIRETRIZES:
            - Seja concisa, assertiva e forneça soluções estruturadas.
            - Você possui acesso a ferramentas reais via MCP (Navegação Web Stealth, Scanner de Rede Local, Sandbox Docker, Google Agenda, Gmail, OneDrive, etc).
            - Utilize as ferramentas quando apropriado para agir no mundo real.
            
            MEMÓRIAS RELEVANTES DO USUÁRIO:
            {memoryContext}
            """;

        // 4. Obter ferramentas registradas
        var availableTools = await _mcpHost.GetAvailableToolsAsync(cancellationToken);

        var llmRequest = new LLMRequest(
            Prompt: request.UserInput,
            SystemPrompt: systemPrompt,
            History: session.Messages.TakeLast(10).ToList(),
            Tools: availableTools
        );

        // 5. Selecionar Provedor com Fallback Inteligente
        var (provider, response) = await ExecuteWithFallbackAsync(request.PreferredProvider, llmRequest, cancellationToken);

        var executedTools = new List<ToolExecutionResult>();

        // 6. Loop de Execução de Ferramentas (Tool Calling)
        if (response.ToolCalls != null && response.ToolCalls.Count > 0)
        {
            onStateChanged?.Invoke(AgentState.Executing, $"Executando {response.ToolCalls.Count} ferramentas MCP...");

            foreach (var toolCall in response.ToolCalls)
            {
                _logger.LogInformation("Executando ferramenta MCP: {ToolName} com args: {Args}", toolCall.ToolName, toolCall.ArgumentsJson);
                var toolResult = await _mcpHost.DispatchToolAsync(toolCall.ToolName, toolCall.ArgumentsJson, cancellationToken);
                executedTools.Add(toolResult);
            }

            // Segunda passagem para a LLM sintetizar os resultados das ferramentas
            var toolSummaryPrompt = $"Resultados das ferramentas executadas:\n" + 
                string.Join("\n", executedTools.Select(t => $"Ferramenta '{t.ToolName}': {t.Output}")) + 
                $"\n\nPergunta original: {request.UserInput}\nSintetize a resposta final para o usuário em PT-BR.";

            var synthesisRequest = new LLMRequest(
                Prompt: toolSummaryPrompt,
                SystemPrompt: systemPrompt,
                History: session.Messages.TakeLast(10).ToList()
            );

            var (_, synthesisResponse) = await ExecuteWithFallbackAsync(request.PreferredProvider, synthesisRequest, cancellationToken);
            response = synthesisResponse;
        }

        // 7. Gravar resposta da assistente
        var assistantMsg = new ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = response.Text,
            ProviderUsed = response.Provider,
            Timestamp = DateTime.UtcNow
        };
        session.Messages.Add(assistantMsg);

        // 8. Síntese de Voz (TTS) opcional
        byte[]? audioBytes = null;
        if (request.EnableVoiceOutput)
        {
            onStateChanged?.Invoke(AgentState.Speaking, "Sintetizando resposta por voz em PT-BR...");
            try
            {
                audioBytes = await _voiceEngine.SynthesizeSpeechAsync(response.Text, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha na síntese de voz. Resposta continuará em texto.");
            }
        }

        onStateChanged?.Invoke(AgentState.Idle, "Aguardando próximo comando.");

        return new ProcessUserInputResponse(
            SessionId: sessionId,
            TextResponse: response.Text,
            ProviderUsed: response.Provider,
            ModelUsed: response.ModelName,
            FinalState: AgentState.Idle,
            ExecutedTools: executedTools,
            RecalledMemories: recalledMemories,
            AudioResponse: audioBytes
        );
    }

    private async Task<(ILLMProvider Provider, LLMResponse Response)> ExecuteWithFallbackAsync(
        LLMProviderType? preferredProvider, 
        LLMRequest request, 
        CancellationToken cancellationToken)
    {
        // Ordenação: 1º preferido pelo usuário, 2º nuvem (OpenAI/Anthropic/Gemini/Groq), 3º Fallback Local (Ollama/Phi-3)
        var orderedProviders = _llmProviders.OrderBy(p => 
            preferredProvider.HasValue && p.ProviderType == preferredProvider.Value ? 0 :
            p.ProviderType == LLMProviderType.OllamaLocal || p.ProviderType == LLMProviderType.Phi3LocalOnnx ? 99 : 1
        ).ToList();

        Exception? lastException = null;

        foreach (var provider in orderedProviders)
        {
            try
            {
                _logger.LogInformation("Tentando gerar resposta com provedor: {ProviderType}", provider.ProviderType);
                var isHealthy = await provider.IsHealthyAsync(cancellationToken);
                if (!isHealthy)
                {
                    _logger.LogWarning("Provedor {ProviderType} não está saudável, tentando próximo...", provider.ProviderType);
                    continue;
                }

                var response = await provider.GenerateAsync(request, cancellationToken);
                return (provider, response);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro com o provedor {ProviderType}. Realizando fallback...", provider.ProviderType);
                lastException = ex;
            }
        }

        // Se todos falharem, cria resposta de contingência inteligente
        return (
            _llmProviders.First(), 
            new LLMResponse(
                Text: "Sistemas operando em modo de contingência local. Não foi possível conectar a servidores externos.",
                Provider: LLMProviderType.OllamaLocal,
                ModelName: "Phi-3-Mini-Local-Fallback",
                PromptTokens: 0,
                CompletionTokens: 0
            )
        );
    }
}
