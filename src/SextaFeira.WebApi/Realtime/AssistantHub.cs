using Microsoft.AspNetCore.SignalR;

namespace SextaFeira.WebApi.Realtime;

/// <summary>Canal em tempo real com cada dispositivo (PC, celular, tablet...).</summary>
public sealed class AssistantHub : Hub
{
    private readonly AssistantSessionService _session;

    public AssistantHub(AssistantSessionService session) => _session = session;

    public Task Register(string deviceId, string name, string kind) =>
        _session.RegisterAsync(Context.ConnectionId, deviceId, name, kind);

    public Task SetMicState(bool active) => _session.SetMicStateAsync(Context.ConnectionId, active);

    /// <summary>Uma fala completa: PCM 16-bit mono 16 kHz (segmentada no cliente por detecção de voz).</summary>
    /// <remarks>Chega em base64 porque o protocolo JSON do SignalR não transporta Uint8Array.</remarks>
    public void SendUtterance(string pcm16Base64)
    {
        // Não bloqueia o canal enquanto transcreve (o SignalR processa uma chamada por vez por cliente).
        var connectionId = Context.ConnectionId;
        var pcm = Convert.FromBase64String(pcm16Base64);
        _ = Task.Run(() => _session.HandleUtteranceAsync(connectionId, pcm));
    }

    public Task SendText(string text) => _session.HandleTextAsync(Context.ConnectionId, text);

    public Task Interrupt() => _session.InterruptAsync();

    public void PlaybackFinished() => _session.PlaybackFinished(Context.ConnectionId);

    public Task ClearConversation() => _session.ClearConversationAsync();

    public override Task OnDisconnectedAsync(Exception? exception) => _session.UnregisterAsync(Context.ConnectionId);
}
