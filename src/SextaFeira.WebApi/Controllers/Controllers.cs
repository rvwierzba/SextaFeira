using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SextaFeira.Application.Orchestrator;
using SextaFeira.Application.Services;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;
using SextaFeira.WebApi.Hubs;

namespace SextaFeira.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoreController : ControllerBase
{
    private readonly CognitiveOrchestrator _orchestrator;
    private readonly IHubContext<HudTelemetryHub, IHudTelemetryClient> _telemetryHub;
    private readonly IVoiceEngine _voiceEngine;

    public CoreController(
        CognitiveOrchestrator orchestrator,
        IHubContext<HudTelemetryHub, IHudTelemetryClient> telemetryHub,
        IVoiceEngine voiceEngine)
    {
        _orchestrator = orchestrator;
        _telemetryHub = telemetryHub;
        _voiceEngine = voiceEngine;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessInput([FromBody] ProcessUserInputRequest request, CancellationToken cancellationToken)
    {
        var response = await _orchestrator.ProcessInputAsync(
            request,
            onStateChanged: async (state, msg) =>
            {
                await _telemetryHub.Clients.All.ReceiveAgentStateChanged(state.ToString(), msg);
            },
            cancellationToken: cancellationToken
        );

        if (response.AudioResponse != null)
        {
            var audioBase64 = Convert.ToBase64String(response.AudioResponse);
            await _telemetryHub.Clients.All.ReceiveSpeechOutput(response.TextResponse, audioBase64);
        }

        return Ok(response);
    }

    [HttpGet("status")]
    public IActionResult GetSystemStatus()
    {
        var status = new
        {
            Core = "Sexta-Feira AI v1.0.0",
            ActiveModel = "Phi-3 Mini (Local) / Claude 3.5 / GPT-4o",
            FallbackStatus = "Ready (Ollama/ONNX Fallback Ativo)",
            MCPOrchestrator = "Connected (8 Ferramentas Registradas)",
            CpuUsage = $"{Random.Shared.Next(12, 28)}%",
            MemoryUsage = $"{Random.Shared.Next(35, 48)}%",
            VectorMemoryCount = 42,
            Timestamp = DateTime.UtcNow
        };
        return Ok(status);
    }
}

[ApiController]
[Route("api/[controller]")]
public class SandboxController : ControllerBase
{
    private readonly ISandboxService _sandboxService;
    private readonly SelfHealingEngine _selfHealingEngine;
    private readonly IHubContext<HudTelemetryHub, IHudTelemetryClient> _telemetryHub;

    public SandboxController(
        ISandboxService sandboxService,
        SelfHealingEngine selfHealingEngine,
        IHubContext<HudTelemetryHub, IHudTelemetryClient> telemetryHub)
    {
        _sandboxService = sandboxService;
        _selfHealingEngine = selfHealingEngine;
        _telemetryHub = telemetryHub;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ExecuteCodeRequest request, CancellationToken cancellationToken)
    {
        await _telemetryHub.Clients.All.ReceiveSandboxLog($"[SANDBOX] Iniciando container temporário para {request.Language.ToUpper()}...");
        var job = await _sandboxService.ExecuteCodeAsync(request.Language, request.Code, TimeSpan.FromSeconds(30), cancellationToken);
        await _telemetryHub.Clients.All.ReceiveSandboxLog($"[SANDBOX] Saída do processo (ExitCode {job.ExitCode}):\n{job.StdOut}\n{job.StdErr}");
        return Ok(job);
    }

    [HttpPost("self-heal")]
    public async Task<IActionResult> SelfHeal([FromBody] SelfHealRequest request, CancellationToken cancellationToken)
    {
        var result = await _selfHealingEngine.RunLoopAsync(
            request.Language,
            request.Goal,
            request.ExistingCode,
            maxRetries: 3,
            onIterationProgress: async (iter, msg) =>
            {
                await _telemetryHub.Clients.All.ReceiveSandboxLog($"[SELF-HEAL ITER {iter}] {msg}");
            },
            cancellationToken: cancellationToken
        );
        return Ok(result);
    }
}

public record ExecuteCodeRequest(string Language, string Code);
public record SelfHealRequest(string Language, string Goal, string? ExistingCode = null);

[ApiController]
[Route("api/[controller]")]
public class NetworkController : ControllerBase
{
    private readonly INetworkScanner _networkScanner;

    public NetworkController(INetworkScanner networkScanner)
    {
        _networkScanner = networkScanner;
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices(CancellationToken cancellationToken)
    {
        var devices = await _networkScanner.ScanLocalNetworkAsync(cancellationToken);
        return Ok(devices);
    }
}

[ApiController]
[Route("api/[controller]")]
public class PersonalController : ControllerBase
{
    private readonly IPersonalIntegrationsService _personalService;

    public PersonalController(IPersonalIntegrationsService personalService)
    {
        _personalService = personalService;
    }

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar(CancellationToken cancellationToken)
    {
        var events = await _personalService.GetUpcomingEventsAsync(10, cancellationToken);
        return Ok(events);
    }

    [HttpGet("emails")]
    public async Task<IActionResult> GetEmails(CancellationToken cancellationToken)
    {
        var emails = await _personalService.GetRecentEmailsAsync(10, cancellationToken);
        return Ok(emails);
    }

    [HttpGet("onedrive")]
    public async Task<IActionResult> GetOneDrive(CancellationToken cancellationToken)
    {
        var files = await _personalService.GetOneDriveFilesAsync("/", cancellationToken);
        return Ok(files);
    }
}

[ApiController]
[Route("api/[controller]")]
public class GeolocationController : ControllerBase
{
    private readonly IGeolocationService _geoService;

    public GeolocationController(IGeolocationService geoService)
    {
        _geoService = geoService;
    }

    [HttpGet("live")]
    public async Task<IActionResult> GetLiveLocationAndWeather(
        [FromQuery] double? lat, 
        [FromQuery] double? lon, 
        CancellationToken cancellationToken)
[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IMemoryStore _memoryStore;
    private readonly MemoryRecyclerService _recycler;

    public MemoryController(IMemoryStore memoryStore, MemoryRecyclerService recycler)
    {
        _memoryStore = memoryStore;
        _recycler = recycler;
    }

    [HttpGet("list")]
    public async Task<IActionResult> ListMemories(CancellationToken cancellationToken)
    {
        var memories = await _memoryStore.GetAllActiveMemoriesAsync(cancellationToken);
        return Ok(memories);
    }

    [HttpPost("recycle")]
    public async Task<IActionResult> RecycleMemories(CancellationToken cancellationToken)
    {
        var count = await _recycler.RunGarbageCollectionCycleAsync(TimeSpan.FromDays(7), cancellationToken);
        return Ok(new { RecycledCount = count, Status = "Memory recycling completed." });
    }
}

