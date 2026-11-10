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
    private readonly IAiConnectionManager? _aiConnectionManager;
    private readonly IOpenJarvisEngine? _openJarvisEngine;
    private readonly ILogger<CognitiveOrchestrator> _logger;

    private static readonly Dictionary<Guid, ConversationSession> _sessionCache = new();

    public CognitiveOrchestrator(
        IEnumerable<ILLMProvider> llmProviders,
        IMemoryStore memoryStore,
        IMcpHost mcpHost,
        IVoiceEngine voiceEngine,
        ILogger<CognitiveOrchestrator> logger,
        IAiConnectionManager? aiConnectionManager = null,
        IOpenJarvisEngine? openJarvisEngine = null)
    {
        _llmProviders = llmProviders;
        _memoryStore = memoryStore;
        _mcpHost = mcpHost;
        _voiceEngine = voiceEngine;
        _logger = logger;
        _aiConnectionManager = aiConnectionManager;
        _openJarvisEngine = openJarvisEngine;
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

        // 3. Montar System Prompt do Sexta-Feira com contexto temporal e de persona em tempo real (Fuso de Brasília / SP)
        TimeZoneInfo brTimeZone;
        try
        {
            brTimeZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");
        }
        catch
        {
            brTimeZone = TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasilia Time", "Brasilia Time");
        }
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, brTimeZone);

        var period = now.Hour switch
        {
            >= 5 and < 12 => "Manhã",
            >= 12 and < 18 => "Tarde",
            _ => "Noite"
        };
        var greeting = period switch
        {
            "Manhã" => "Bom dia",
            "Tarde" => "Boa tarde",
            _ => "Boa noite"
        };

        var systemPrompt = $"""
            Você é a SEXTA-FEIRA (F.R.I.D.A.Y.), a assistente pessoal de inteligência artificial de Rafael Wierzba, inspirada na IA do Homem de Ferro.
            Você é inteligente, perspicaz, carismática e tem uma conversa 100% fluida, humana e natural.
            
            INFORMAÇÕES DE SISTEMA:
            - Data e Hora Atual: {now:dd/MM/yyyy HH:mm} ({period}). Se Rafael perguntar que horas são, diga a hora com naturalidade ({now:HH:mm}).
            - Usuário: Rafael Wierzba.
            
            DIRETRIZES DE CONVERSAÇÃO (ESTILO JARVIS):
            - Fale como uma pessoa real conversando em áudio ao vivo: com naturalidade, carisma e empatia.
            - Responda em 1 ou no máximo 2 frases conversacionais curtas, ágeis e diretas ao ponto.
            - NUNCA use formatação de texto escrita (sem asteriscos **, sem tópicos, sem traços e sem listas).
            - NUNCA fique repetindo localização, cidades, canal ou horário a menos que Rafael pergunte explicitamente sobre isso!
            - NUNCA termine suas respostas com perguntas robóticas vazias (como "Como posso te ajudar hoje?", "Em que posso ser útil?" ou "O que deseja fazer?"). Apenas responda ao que ele disse de forma objetiva e conclua.
            - Seja confiante, ágil e resolutiva, com a mesma elegância e inteligência da assistente do Homem de Ferro.
            
            MEMÓRIAS DO USUÁRIO:
            {memoryContext}
            """;

        // 4. Obter ferramentas registradas
        var availableTools = await _mcpHost.GetAvailableToolsAsync(cancellationToken);

        var effectiveProvider = request.PreferredProvider;
        if (!effectiveProvider.HasValue && _aiConnectionManager != null)
        {
            effectiveProvider = await _aiConnectionManager.GetActiveProviderTypeAsync(cancellationToken);
        }

        // Detecção de comando de voz para troca dinâmica do cérebro de IA
        var lowerInput = request.UserInput.ToLowerInvariant();
        if (lowerInput.Contains("usar claude") || lowerInput.Contains("mudar para o claude") || lowerInput.Contains("mude para o claude") || lowerInput.Contains("ativar claude") || lowerInput.Contains("trocar para claude"))
        {
            if (_aiConnectionManager != null)
            {
                await _aiConnectionManager.SetActiveProviderAsync(LLMProviderType.Anthropic, cancellationToken);
                effectiveProvider = LLMProviderType.Anthropic;
            }
        }
        else if (lowerInput.Contains("usar openai") || lowerInput.Contains("usar chatgpt") || lowerInput.Contains("mudar para o gpt") || lowerInput.Contains("mude para o gpt") || lowerInput.Contains("ativar gpt") || lowerInput.Contains("trocar para chatgpt"))
        {
            if (_aiConnectionManager != null)
            {
                await _aiConnectionManager.SetActiveProviderAsync(LLMProviderType.OpenAI, cancellationToken);
                effectiveProvider = LLMProviderType.OpenAI;
            }
        }
        else if (lowerInput.Contains("usar gemini") || lowerInput.Contains("mudar para o gemini") || lowerInput.Contains("mude para o gemini") || lowerInput.Contains("ativar gemini") || lowerInput.Contains("trocar para gemini"))
        {
            if (_aiConnectionManager != null)
            {
                await _aiConnectionManager.SetActiveProviderAsync(LLMProviderType.GoogleGemini, cancellationToken);
                effectiveProvider = LLMProviderType.GoogleGemini;
            }
        }
        else if (lowerInput.Contains("usar openjarvis") || lowerInput.Contains("ativar openjarvis") || lowerInput.Contains("modo openjarvis") || lowerInput.Contains("usar local") || lowerInput.Contains("modo local") || lowerInput.Contains("modo stanford"))
        {
            if (_aiConnectionManager != null)
            {
                await _aiConnectionManager.SetActiveProviderAsync(LLMProviderType.OpenJarvisLocal, cancellationToken);
            }
            effectiveProvider = LLMProviderType.OpenJarvisLocal;
        }

        var llmRequest = new LLMRequest(
            Prompt: request.UserInput,
            SystemPrompt: systemPrompt,
            History: session.Messages.TakeLast(20).ToList(),
            Tools: availableTools
        );

        // 5. Selecionar Provedor com Fallback Inteligente ou OpenJarvis Spec Search Loop
        LLMResponse response;
        if ((effectiveProvider == LLMProviderType.OpenJarvisLocal || effectiveProvider == LLMProviderType.OllamaLocal) && _openJarvisEngine != null)
        {
            onStateChanged?.Invoke(AgentState.Thinking, "OpenJarvis: Executando ciclo de Spec Search e inferência on-device...");
            var specPlan = await _openJarvisEngine.SynthesizeSpecAsync(request.UserInput, availableTools, cancellationToken);
            response = await _openJarvisEngine.ExecuteSpecPlanAsync(specPlan, llmRequest, cancellationToken);
        }
        else
        {
            var (_, fallbackResponse) = await ExecuteWithFallbackAsync(effectiveProvider, llmRequest, cancellationToken);
            response = fallbackResponse;
        }

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

            // Síntese inteligente e natural dos resultados das ferramentas
            var toolSummaryText = FormatToolResultsToNaturalLanguage(request.UserInput, executedTools);
            response = new LLMResponse(
                Text: toolSummaryText,
                Provider: response.Provider,
                ModelName: response.ModelName,
                PromptTokens: response.PromptTokens,
                CompletionTokens: response.CompletionTokens
            );
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

    private static string FormatToolResultsToNaturalLanguage(string userInput, List<ToolExecutionResult> executedTools)
    {
        var summaries = new List<string>();

        foreach (var tool in executedTools)
        {
            if (tool.ToolName.Equals("calendar_get_upcoming", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var events = JsonSerializer.Deserialize<List<CalendarEventItem>>(tool.Output);
                    if (events != null && events.Count > 0)
                    {
                        var eventListStr = string.Join("; ", events.Select(e => $"'{e.Title}' às {e.StartTime:HH:mm} ({e.Location})"));
                        summaries.Add($"Consultando sua agenda do Google: foram identificados {events.Count} compromissos: {eventListStr}.");
                    }
                    else
                    {
                        summaries.Add("Sua agenda do Google não possui compromissos pendentes para hoje.");
                    }
                }
                catch
                {
                    summaries.Add($"Agenda sincronizada: {tool.Output}");
                }
            }
            else if (tool.ToolName.Equals("network_scan", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var devices = JsonSerializer.Deserialize<List<NetworkDevice>>(tool.Output);
                    if (devices != null && devices.Count > 0)
                    {
                        var activeCount = devices.Count(d => d.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));
                        var names = string.Join(", ", devices.Take(3).Select(d => $"{d.Hostname} ({d.IpAddress})"));
                        summaries.Add($"Varredura de rede concluída: {devices.Count} nós identificados ({activeCount} ativos), incluindo: {names}.");
                    }
                    else
                    {
                        summaries.Add("Varredura de rede concluída. Todos os parâmetros nominais.");
                    }
                }
                catch
                {
                    summaries.Add($"Scanner de rede: {tool.Output}");
                }
            }
            else if (tool.ToolName.Equals("email_get_recent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var emails = JsonSerializer.Deserialize<List<EmailMessageItem>>(tool.Output);
                    if (emails != null && emails.Count > 0)
                    {
                        var unread = emails.Count(e => e.IsUnread);
                        summaries.Add($"Caixa de entrada Gmail verificada: {emails.Count} e-mails recentes ({unread} não lidos). Último assunto: '{emails.First().Subject}'.");
                    }
                }
                catch
                {
                    summaries.Add($"E-mails verificados: {tool.Output}");
                }
            }
            else if (tool.ToolName.Equals("spotify_play", StringComparison.OrdinalIgnoreCase))
            {
                summaries.Add($"Comando de áudio executado: {tool.Output}");
            }
            else if (tool.ToolName.Equals("get_geolocation_weather", StringComparison.OrdinalIgnoreCase))
            {
                summaries.Add($"Telemetria climática e localização atualizadas: {tool.Output}");
            }
            else
            {
                summaries.Add($"Ferramenta '{tool.ToolName}' executada com sucesso: {tool.Output}");
            }
        }

        return string.Join(" ", summaries);
    }

    private async Task<(ILLMProvider Provider, LLMResponse Response)> ExecuteWithFallbackAsync(
        LLMProviderType? preferredProvider, 
        LLMRequest request, 
        CancellationToken cancellationToken)
    {
        var orderedProviders = _llmProviders.OrderBy(p => 
            preferredProvider.HasValue && p.ProviderType == preferredProvider.Value ? 0 :
            p.ProviderType == LLMProviderType.OllamaLocal || p.ProviderType == LLMProviderType.Phi3LocalOnnx ? 99 : 1
        ).ToList();

        foreach (var provider in orderedProviders)
        {
            try
            {
                var isHealthy = await provider.IsHealthyAsync(cancellationToken);
                if (!isHealthy) continue;

                var response = await provider.GenerateAsync(request, cancellationToken);
                return (provider, response);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro com o provedor {ProviderType}. Realizando fallback...", provider.ProviderType);
            }
        }

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

