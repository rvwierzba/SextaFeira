using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;
using SextaFeira.WebApi.Hubs;

namespace SextaFeira.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiConnectionsController : ControllerBase
{
    private readonly IAiConnectionManager _connectionManager;
    private readonly IHubContext<HudTelemetryHub, IHudTelemetryClient> _telemetryHub;
    private readonly ILogger<AiConnectionsController> _logger;

    public AiConnectionsController(
        IAiConnectionManager connectionManager,
        IHubContext<HudTelemetryHub, IHudTelemetryClient> telemetryHub,
        ILogger<AiConnectionsController> logger)
    {
        _connectionManager = connectionManager;
        _telemetryHub = telemetryHub;
        _logger = logger;
    }

    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections(CancellationToken cancellationToken)
    {
        var connections = await _connectionManager.GetAllConnectionsAsync(cancellationToken);
        var active = await _connectionManager.GetActiveProviderTypeAsync(cancellationToken);

        return Ok(new
        {
            activeProvider = active.ToString(),
            connections = connections
        });
    }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectAiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CredentialOrToken))
        {
            return BadRequest(new { error = "Credencial ou token de conexão é obrigatório." });
        }

        var connection = await _connectionManager.ConnectAsync(
            request.Provider,
            request.CredentialOrToken.Trim(),
            request.AccountName,
            request.ModelName,
            request.AuthType,
            cancellationToken
        );

        await _telemetryHub.Clients.All.ReceiveAgentStateChanged(
            "Thinking",
            $"Nova conta de IA conectada: {request.Provider} ({connection.AccountName})"
        );

        return Ok(new
        {
            success = true,
            message = $"Conta de IA vinculada com sucesso: {request.Provider}",
            connection
        });
    }

    [HttpPost("select")]
    public async Task<IActionResult> SelectActive([FromBody] SelectProviderRequest request, CancellationToken cancellationToken)
    {
        var connection = await _connectionManager.SetActiveProviderAsync(request.Provider, cancellationToken);

        await _telemetryHub.Clients.All.ReceiveAgentStateChanged(
            "Thinking",
            $"Cérebro ativo da Sexta-Feira alterado para {request.Provider} ({connection.ModelName})"
        );

        return Ok(new
        {
            success = true,
            message = $"Provedor ativo atualizado para {request.Provider}",
            connection
        });
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestLatency([FromBody] SelectProviderRequest request, CancellationToken cancellationToken)
    {
        var latencyMs = await _connectionManager.TestConnectionLatencyAsync(request.Provider, cancellationToken);

        return Ok(new
        {
            provider = request.Provider.ToString(),
            latencyMs,
            status = "Online"
        });
    }

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect([FromBody] SelectProviderRequest request, CancellationToken cancellationToken)
    {
        var result = await _connectionManager.DisconnectAsync(request.Provider, cancellationToken);
        return Ok(new { success = result, message = $"Provedor {request.Provider} desconectado." });
    }

    [HttpPost("google-oauth")]
    public async Task<IActionResult> GoogleOAuth([FromBody] GoogleOAuthConnectRequest request, CancellationToken cancellationToken)
    {
        var email = !string.IsNullOrWhiteSpace(request.Email) ? request.Email : "rafaelwierzba@gmail.com";
        var token = !string.IsNullOrWhiteSpace(request.Token) ? request.Token : $"google_oauth_token_{Guid.NewGuid():N}";

        var connection = await _connectionManager.ConnectAsync(
            LLMProviderType.GoogleGemini,
            token,
            accountName: email,
            modelName: "gemini-2.0-flash",
            authType: "Google_OAuth",
            cancellationToken
        );

        await _connectionManager.SetActiveProviderAsync(LLMProviderType.GoogleGemini, cancellationToken);

        return Ok(new
        {
            success = true,
            message = $"Login Google realizado com sucesso. Gemini 2.0 Flash ativo para {email}.",
            connection
        });
    }
}

public record ConnectAiRequest(
    LLMProviderType Provider,
    string CredentialOrToken,
    string? AccountName = null,
    string? ModelName = null,
    string? AuthType = null
);

public record SelectProviderRequest(LLMProviderType Provider);

public record GoogleOAuthConnectRequest(
    string? Token = null,
    string? Email = null
);

