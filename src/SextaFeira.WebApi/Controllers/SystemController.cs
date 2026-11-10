using Microsoft.AspNetCore.Mvc;
using SextaFeira.Application.Agent;
using SextaFeira.Domain.Agent;
using SextaFeira.Infrastructure.Mcp;
using SextaFeira.Infrastructure.Storage;
using SextaFeira.WebApi.Hosting;
using SextaFeira.WebApi.Realtime;

namespace SextaFeira.WebApi.Controllers;

[ApiController]
[Route("api")]
public sealed class SystemController : ControllerBase
{
    private const string Mask = "••••";

    private readonly ISettingsStore _settings;
    private readonly EngineRouter _router;
    private readonly ToolRegistry _tools;
    private readonly ISpeechToText _stt;
    private readonly ITextToSpeech _tts;
    private readonly McpManager _mcp;
    private readonly IMemoryStore _memory;
    private readonly AssistantSessionService _session;
    private readonly IConfiguration _config;

    public SystemController(ISettingsStore settings, EngineRouter router, ToolRegistry tools, ISpeechToText stt, ITextToSpeech tts,
        McpManager mcp, IMemoryStore memory, AssistantSessionService session, IConfiguration config)
    {
        _settings = settings;
        _router = router;
        _tools = tools;
        _stt = stt;
        _tts = tts;
        _mcp = mcp;
        _memory = memory;
        _session = session;
        _config = config;
    }

    /// <summary>Estado real de tudo: engines, voz, MCP, dispositivos e endereços de rede.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var engines = await Task.WhenAll(_router.All.Select(async e =>
        {
            var h = await e.CheckHealthAsync(ct);
            return new { e.Id, e.DisplayName, e.IsLocal, model = e.CurrentModel, h.Available, h.Detail, h.Models };
        }));
        var httpsPort = _config.GetValue("HttpsPort", 5001);
        var tools = _tools.GetAll();
        return Ok(new
        {
            activeEngine = _router.ResolveActive().Id,
            engines,
            stt = new { ready = _stt.IsReady, status = _stt.Status },
            tts = new { status = _tts.Status },
            mcp = _mcp.GetStatus(),
            tools = tools.Select(t => new { t.Spec.Name, t.Source }),
            devices = _session.Devices.Select(d => new { d.DeviceId, d.Name, d.Kind, d.MicActive }),
            lanUrls = _config.GetValue("ListenOnLan", true)
                ? LanCertificate.LanAddresses().Select(ip => $"https://{ip}:{httpsPort}")
                : [],
        });
    }

    [HttpGet("settings")]
    public IActionResult GetSettings() => Ok(MaskKeys(JsonSettingsStore.Clone(_settings.Current)));

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] AssistantSettings incoming, CancellationToken ct)
    {
        var current = _settings.Current;
        // Campos mascarados significam "não alterar".
        incoming.OpenAI.ApiKey = Unmask(incoming.OpenAI.ApiKey, current.OpenAI.ApiKey);
        incoming.Anthropic.ApiKey = Unmask(incoming.Anthropic.ApiKey, current.Anthropic.ApiKey);
        incoming.Gemini.ApiKey = Unmask(incoming.Gemini.ApiKey, current.Gemini.ApiKey);
        if (_router.Find(incoming.ActiveEngine) == null)
            return BadRequest(new { error = $"Engine desconhecida: {incoming.ActiveEngine}" });

        await _settings.SaveAsync(incoming, ct);
        return Ok(MaskKeys(JsonSettingsStore.Clone(incoming)));
    }

    [HttpGet("mcp/config")]
    public IActionResult GetMcpConfig() => Content(_mcp.ReadConfigText(), "application/json");

    [HttpPut("mcp/config")]
    public async Task<IActionResult> SaveMcpConfig(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        try { await _mcp.SaveConfigTextAsync(json, ct); }
        catch (Exception ex) { return BadRequest(new { error = $"JSON inválido: {ex.Message}" }); }
        return Ok(_mcp.GetStatus());
    }

    [HttpPost("mcp/reload")]
    public async Task<IActionResult> ReloadMcp(CancellationToken ct)
    {
        await _mcp.ReloadAsync(ct);
        return Ok(_mcp.GetStatus());
    }

    [HttpGet("memory")]
    public async Task<IActionResult> ListMemory(CancellationToken ct) => Ok(await _memory.ListAsync(ct));

    [HttpDelete("memory/{id}")]
    public async Task<IActionResult> DeleteMemory(string id, CancellationToken ct) =>
        await _memory.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Teste rápido da voz: /api/tts?text=Olá</summary>
    [HttpGet("tts")]
    public async Task<IActionResult> Tts([FromQuery] string text, CancellationToken ct)
    {
        var audio = await _tts.SynthesizeAsync(text, ct);
        return audio.Length > 0 ? File(audio, "audio/mpeg") : StatusCode(503, new { error = _tts.Status });
    }

    /// <summary>Certificado da rede local, para instalar como confiável no celular (opcional).</summary>
    [HttpGet("/sextafeira-lan.crt")]
    public IActionResult Certificate() =>
        System.IO.File.Exists(LanCertificate.CrtPath)
            ? PhysicalFile(LanCertificate.CrtPath, "application/x-x509-ca-cert", "sextafeira-lan.crt")
            : NotFound();

    private static AssistantSettings MaskKeys(AssistantSettings s)
    {
        s.OpenAI.ApiKey = MaskKey(s.OpenAI.ApiKey);
        s.Anthropic.ApiKey = MaskKey(s.Anthropic.ApiKey);
        s.Gemini.ApiKey = MaskKey(s.Gemini.ApiKey);
        return s;
    }

    private static string MaskKey(string key) => string.IsNullOrEmpty(key) ? "" : Mask + key[^Math.Min(4, key.Length)..];

    private static string Unmask(string incoming, string current) =>
        incoming.StartsWith(Mask, StringComparison.Ordinal) ? current : incoming.Trim();
}
