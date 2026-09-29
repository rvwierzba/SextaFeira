using Microsoft.AspNetCore.SignalR;
using SextaFeira.Domain.Enums;

namespace SextaFeira.WebApi.Hubs;

public interface IHudTelemetryClient
{
    Task ReceiveAgentStateChanged(string state, string description);
    Task ReceiveTelemetryMetrics(object metrics);
    Task ReceiveSandboxLog(string logLine);
    Task ReceiveNetworkDeviceDiscovered(object device);
    Task ReceiveSpeechOutput(string text, string? audioBase64);
}

public class HudTelemetryHub : Hub<IHudTelemetryClient>
{
    public async Task BroadcastState(AgentState state, string message)
    {
        await Clients.All.ReceiveAgentStateChanged(state.ToString(), message);
    }

    public async Task ReportSandboxOutput(string line)
    {
        await Clients.All.ReceiveSandboxLog(line);
    }
}

public class VoiceStreamHub : Hub
{
    public async Task SendAudioChunk(byte[] chunk)
    {
        // Broadcast ou processamento de chunk de áudio para STT
        await Clients.Others.SendAsync("AudioChunkReceived", chunk);
    }

    public async Task TriggerWakeWordDetected(string keyword)
    {
        await Clients.All.SendAsync("WakeWordActivated", keyword);
    }
}
