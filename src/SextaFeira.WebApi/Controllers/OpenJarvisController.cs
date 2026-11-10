using Microsoft.AspNetCore.Mvc;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpenJarvisController : ControllerBase
{
    private readonly IOpenJarvisEngine _openJarvisEngine;
    private readonly IMcpHost _mcpHost;
    private readonly ILogger<OpenJarvisController> _logger;

    public OpenJarvisController(
        IOpenJarvisEngine openJarvisEngine,
        IMcpHost mcpHost,
        ILogger<OpenJarvisController> logger)
    {
        _openJarvisEngine = openJarvisEngine;
        _mcpHost = mcpHost;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _openJarvisEngine.GetSystemStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        var catalog = await _openJarvisEngine.GetIntelligenceCatalogAsync(cancellationToken);
        return Ok(catalog);
    }

    [HttpGet("hardware")]
    public async Task<IActionResult> GetHardware(CancellationToken cancellationToken)
    {
        var hardware = await _openJarvisEngine.ProbeHardwareAsync(cancellationToken);
        return Ok(hardware);
    }

    [HttpGet("traces")]
    public async Task<IActionResult> GetTraces([FromQuery] int count = 20, CancellationToken cancellationToken = default)
    {
        var traces = await _openJarvisEngine.GetRecentTracesAsync(count, cancellationToken);
        return Ok(traces);
    }

    public record SpecSearchRequest(string Goal);

    [HttpPost("spec-search")]
    public async Task<IActionResult> SynthesizeSpec([FromBody] SpecSearchRequest request, CancellationToken cancellationToken)
    {
        var tools = await _mcpHost.GetAvailableToolsAsync(cancellationToken);
        var plan = await _openJarvisEngine.SynthesizeSpecAsync(request.Goal, tools, cancellationToken);
        return Ok(plan);
    }

    public record FastExecuteRequest(string Prompt, string? SystemPrompt = null);

    [HttpPost("execute-fast")]
    public async Task<IActionResult> ExecuteFast([FromBody] FastExecuteRequest request, CancellationToken cancellationToken)
    {
        var llmReq = new LLMRequest(
            Prompt: request.Prompt,
            SystemPrompt: request.SystemPrompt
        );
        var res = await _openJarvisEngine.ExecuteLocalFastAsync(llmReq, cancellationToken);
        return Ok(res);
    }

    public record SetModeRequest(string Mode);

    [HttpPost("mode")]
    public async Task<IActionResult> SetMode([FromBody] SetModeRequest request, CancellationToken cancellationToken)
    {
        await _openJarvisEngine.SetActiveModeAsync(request.Mode, cancellationToken);
        return Ok(new { Success = true, ActiveMode = request.Mode });
    }
}
