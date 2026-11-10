using SextaFeira.Application.Agent;
using SextaFeira.Domain.Agent;

namespace SextaFeira.WebApi.Hosting;

/// <summary>
/// Carrega o modelo local na GPU e pré-processa o prompt fixo assim que o servidor sobe,
/// para a primeira resposta não demorar 20-30 segundos.
/// </summary>
public sealed class EngineWarmupService : BackgroundService
{
    private readonly EngineRouter _router;
    private readonly ToolRegistry _tools;
    private readonly ISettingsStore _settings;
    private readonly ISpeechToText _stt;
    private readonly ILogger<EngineWarmupService> _logger;

    public EngineWarmupService(EngineRouter router, ToolRegistry tools, ISettingsStore settings, ISpeechToText stt, ILogger<EngineWarmupService> logger)
    {
        _stt = stt;
        _router = router;
        _tools = tools;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // deixa os servidores MCP conectarem primeiro
        // Carregar Whisper (Vulkan) e o LLM (CUDA) na GPU ao mesmo tempo pode travar o driver: um de cada vez.
        for (var i = 0; i < 120 && !_stt.IsReady; i++) await Task.Delay(1000, stoppingToken);
        var engine = _router.Local;
        if (!(await engine.CheckHealthAsync(stoppingToken)).Available) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var tools = _tools.GetAll();
            var request = new ChatRequest(SystemPromptBuilder.BuildStatic(_settings.Current, tools),
                [AgentMessage.User("[contexto: aquecimento] Responda só: ok")], tools.Select(t => t.Spec).ToList(), MaxTokens: 1);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            await foreach (var _ in engine.StreamChatAsync(request, timeout.Token)) { }
            _logger.LogInformation("Modelo local {Model} aquecido em {Ms} ms.", engine.CurrentModel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Falha ao aquecer o modelo local.");
        }
    }
}
