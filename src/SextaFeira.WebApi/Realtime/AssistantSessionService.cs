using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using SextaFeira.Application.Agent;
using SextaFeira.Domain.Agent;

namespace SextaFeira.WebApi.Realtime;

public sealed class DeviceInfo
{
    public required string ConnectionId { get; init; }
    public required string DeviceId { get; init; }
    public string Name { get; set; } = "Dispositivo";
    public string Kind { get; set; } = "desktop";
    public bool MicActive { get; set; }
    public DateTime ConnectedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime FollowUpUntilUtc { get; set; } = DateTime.MinValue;
}

/// <summary>
/// Coordena a conversa entre todos os dispositivos: recebe fala/texto, decide se é para a assistente
/// (palavra de ativação / janela de continuação), roda o agente e devolve texto + áudio frase a frase.
/// </summary>
public sealed class AssistantSessionService
{
    private const int MinUtteranceBytes = 16000 * 2 * 3 / 10; // 300 ms

    private readonly IHubContext<AssistantHub> _hub;
    private readonly IServiceScopeFactory _scopes;
    private readonly ISpeechToText _stt;
    private readonly ITextToSpeech _tts;
    private readonly ISettingsStore _settings;
    private readonly ILogger<AssistantSessionService> _logger;
    private readonly ConcurrentDictionary<string, DeviceInfo> _devices = new();
    private readonly object _turnLock = new();
    private CancellationTokenSource? _activeTurn;
    private string _activeTurnSpokenText = "";

    public Conversation Conversation { get; } = new("principal");

    public AssistantSessionService(IHubContext<AssistantHub> hub, IServiceScopeFactory scopes, ISpeechToText stt,
        ITextToSpeech tts, ISettingsStore settings, ILogger<AssistantSessionService> logger)
    {
        _hub = hub;
        _scopes = scopes;
        _stt = stt;
        _tts = tts;
        _settings = settings;
        _logger = logger;
    }

    public IReadOnlyList<DeviceInfo> Devices => _devices.Values.OrderBy(d => d.ConnectedAtUtc).ToList();

    // ── Dispositivos ─────────────────────────────────────────────────────────

    public async Task RegisterAsync(string connectionId, string deviceId, string name, string kind)
    {
        _devices[connectionId] = new DeviceInfo { ConnectionId = connectionId, DeviceId = deviceId, Name = name, Kind = kind };
        await BroadcastDevicesAsync();
    }

    public async Task UnregisterAsync(string connectionId)
    {
        _devices.TryRemove(connectionId, out _);
        _slots.TryRemove(connectionId, out _);
        await BroadcastDevicesAsync();
    }

    public async Task SetMicStateAsync(string connectionId, bool active)
    {
        if (_devices.TryGetValue(connectionId, out var d)) d.MicActive = active;
        await BroadcastDevicesAsync();
    }

    public void PlaybackFinished(string connectionId)
    {
        if (_devices.TryGetValue(connectionId, out var d))
            d.FollowUpUntilUtc = DateTime.UtcNow.AddSeconds(_settings.Current.Voice.FollowUpWindowSeconds);
    }

    private Task BroadcastDevicesAsync() =>
        _hub.Clients.All.SendAsync("devices", Devices.Select(d => new { d.DeviceId, d.Name, d.Kind, d.MicActive }));

    // ── Entrada ──────────────────────────────────────────────────────────────

    // Fila por dispositivo com "a mais recente vence": enquanto uma fala é transcrita, só a última que chegar
    // fica esperando. Evita acumular minutos de atraso quando há ruído/mídia de fundo.
    private sealed class UtteranceSlot { public bool Busy; public byte[]? Pending; }
    private readonly ConcurrentDictionary<string, UtteranceSlot> _slots = new();

    public async Task HandleUtteranceAsync(string connectionId, byte[] pcm16)
    {
        if (pcm16.Length < MinUtteranceBytes) return;
        var slot = _slots.GetOrAdd(connectionId, _ => new UtteranceSlot());
        lock (slot)
        {
            if (slot.Busy)
            {
                if (slot.Pending != null) _logger.LogDebug("Descartando fala antiga pendente de {Connection}", connectionId);
                slot.Pending = pcm16;
                return;
            }
            slot.Busy = true;
        }

        var next = pcm16;
        while (next != null)
        {
            try { await ProcessUtteranceAsync(connectionId, next); }
            catch (Exception ex) { _logger.LogWarning(ex, "Falha ao processar fala"); }
            lock (slot)
            {
                next = slot.Pending;
                slot.Pending = null;
                if (next == null) slot.Busy = false;
            }
        }
    }

    private async Task ProcessUtteranceAsync(string connectionId, byte[] pcm16)
    {
        if (!_devices.TryGetValue(connectionId, out var device)) return;

        if (!_stt.IsReady)
        {
            await _hub.Clients.Client(connectionId).SendAsync("notice", $"Reconhecimento de voz ainda não está pronto: {_stt.Status}");
            return;
        }

        await _hub.Clients.Client(connectionId).SendAsync("state", new { state = "TRANSCRIBING", detail = "Entendendo...", deviceId = device.DeviceId });

        TranscriptionResult tr;
        try { tr = await _stt.TranscribeAsync(pcm16); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no STT");
            await _hub.Clients.Client(connectionId).SendAsync("notice", "Falha ao transcrever o áudio.");
            return;
        }

        var text = tr.Text.Trim();
        _logger.LogInformation("[{Device}] ouviu ({Ms} ms): {Text}", device.Name, (int)tr.Elapsed.TotalMilliseconds, text);

        if (text.Length == 0 || tr.NoSpeechProbability > 0.85)
        {
            await SendTranscriptAsync(device, text, false, "silêncio");
            await SendIdleIfNoTurnAsync(connectionId, device);
            return;
        }

        // Eco da própria voz da assistente captado pelo microfone.
        if (IsEcho(text))
        {
            await SendTranscriptAsync(device, text, false, "eco");
            return;
        }

        var voice = _settings.Current.Voice;
        var inFollowUp = DateTime.UtcNow <= device.FollowUpUntilUtc || IsTurnActive;
        var hasWake = WakeWord.TryStrip(text, voice.WakeWords, out var command);

        if (voice.RequireWakeWord && !hasWake && !inFollowUp)
        {
            await SendTranscriptAsync(device, text, false, "sem palavra de ativação");
            await SendIdleIfNoTurnAsync(connectionId, device);
            return;
        }

        if (hasWake && (command.Length == 0 || WakeWord.IsOnlyFiller(command)))
        {
            // Só chamou pelo nome: responde curto e abre a janela de conversa.
            await SendTranscriptAsync(device, text, true, null);
            device.FollowUpUntilUtc = DateTime.UtcNow.AddSeconds(voice.FollowUpWindowSeconds);
            await SpeakShortAsync(device, PickAcknowledgement());
            return;
        }

        var finalText = hasWake ? command : text;
        await SendTranscriptAsync(device, finalText, true, null);
        StartTurn(device, finalText);
    }

    public async Task HandleTextAsync(string connectionId, string text)
    {
        if (!_devices.TryGetValue(connectionId, out var device) || string.IsNullOrWhiteSpace(text)) return;
        await SendTranscriptAsync(device, text.Trim(), true, null);
        StartTurn(device, text.Trim());
    }

    public async Task InterruptAsync()
    {
        CancelActiveTurn();
        await _hub.Clients.All.SendAsync("stopAudio");
    }

    public async Task ClearConversationAsync()
    {
        CancelActiveTurn();
        Conversation.Clear();
        await _hub.Clients.All.SendAsync("conversationCleared");
    }

    // ── Turno do agente ──────────────────────────────────────────────────────

    private bool IsTurnActive { get { lock (_turnLock) return _activeTurn is { IsCancellationRequested: false }; } }

    private void CancelActiveTurn()
    {
        lock (_turnLock)
        {
            _activeTurn?.Cancel();
            _activeTurn = null;
        }
    }

    private void StartTurn(DeviceInfo device, string text)
    {
        CancellationTokenSource cts;
        lock (_turnLock)
        {
            _activeTurn?.Cancel(); // nova fala interrompe a resposta anterior (barge-in)
            cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            _activeTurn = cts;
            _activeTurnSpokenText = "";
        }
        _ = _hub.Clients.All.SendAsync("stopAudio");
        _ = Task.Run(() => RunTurnAsync(device, text, cts));
    }

    private async Task RunTurnAsync(DeviceInfo device, string text, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        var turnId = Guid.NewGuid().ToString("N")[..8];
        var audioQueue = Channel.CreateUnbounded<(int Seq, string Text, Task<byte[]> Audio)>();
        var seq = 0;

        // Envia o áudio na ordem certa, enquanto o próximo trecho já está sendo sintetizado.
        var audioSender = Task.Run(async () =>
        {
            await foreach (var item in audioQueue.Reader.ReadAllAsync(ct))
            {
                byte[] audio;
                try { audio = await item.Audio; } catch (OperationCanceledException) { return; } catch { audio = []; }
                ct.ThrowIfCancellationRequested();
                await _hub.Clients.All.SendAsync("assistantAudio", new
                {
                    turnId, seq = item.Seq, text = item.Text, targetDeviceId = device.DeviceId,
                    audio = audio.Length > 0 ? Convert.ToBase64String(audio) : null,
                }, ct);
            }
        }, ct);

        try
        {
            using var scope = _scopes.CreateScope();
            var agent = scope.ServiceProvider.GetRequiredService<ConversationAgent>();
            var context = new ToolContext(Conversation.Id, device.DeviceId);

            await foreach (var evt in agent.RunTurnAsync(Conversation, text, context, device.Name, ct))
            {
                switch (evt)
                {
                    case AgentStatus s:
                        await _hub.Clients.All.SendAsync("state", new { state = s.State, detail = s.Detail, deviceId = device.DeviceId }, ct);
                        break;
                    case AgentTextDelta d:
                        await _hub.Clients.All.SendAsync("assistantDelta", new { turnId, text = d.Text }, ct);
                        break;
                    case AgentSentence sentence:
                        lock (_turnLock) _activeTurnSpokenText += " " + sentence.Text;
                        audioQueue.Writer.TryWrite((seq++, sentence.Text, _tts.SynthesizeAsync(sentence.Text, ct)));
                        break;
                    case AgentToolStarted t:
                        await _hub.Clients.All.SendAsync("tool", new { turnId, name = t.Name, phase = "start", args = t.Arguments }, ct);
                        break;
                    case AgentToolFinished t:
                        await _hub.Clients.All.SendAsync("tool", new { turnId, name = t.Name, phase = "end", ok = t.Success, preview = t.Preview, ms = (int)t.ElapsedMs }, ct);
                        break;
                    case AgentTurnCompleted done:
                        audioQueue.Writer.TryComplete();
                        await audioSender;
                        // Abre a janela de continuação já contando o tempo estimado da fala (~65 ms por caractere);
                        // o aviso PlaybackFinished do dispositivo depois ajusta com precisão.
                        device.FollowUpUntilUtc = DateTime.UtcNow
                            .AddMilliseconds(done.Text.Length * 65)
                            .AddSeconds(_settings.Current.Voice.FollowUpWindowSeconds);
                        await _hub.Clients.All.SendAsync("assistantDone", new
                        {
                            turnId, text = done.Text, engine = done.Engine, model = done.Model,
                            ttftMs = done.TimeToFirstTokenMs, totalMs = done.TotalMs,
                            targetDeviceId = device.DeviceId, audioCount = seq,
                        }, CancellationToken.None);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            await _hub.Clients.All.SendAsync("assistantDone", new { turnId, text = "", cancelled = true, targetDeviceId = device.DeviceId, audioCount = 0 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no turno");
            await _hub.Clients.All.SendAsync("notice", $"Erro: {ex.Message}");
            await _hub.Clients.All.SendAsync("assistantDone", new { turnId, text = "", cancelled = true, targetDeviceId = device.DeviceId, audioCount = 0 });
        }
        finally
        {
            audioQueue.Writer.TryComplete();
            lock (_turnLock) { if (_activeTurn == cts) _activeTurn = null; }
            cts.Dispose();
        }
    }

    private async Task SpeakShortAsync(DeviceInfo device, string text)
    {
        var turnId = Guid.NewGuid().ToString("N")[..8];
        var audio = await _tts.SynthesizeAsync(text);
        await _hub.Clients.All.SendAsync("assistantDelta", new { turnId, text });
        await _hub.Clients.All.SendAsync("assistantAudio", new
        {
            turnId, seq = 0, text, targetDeviceId = device.DeviceId,
            audio = audio.Length > 0 ? Convert.ToBase64String(audio) : null,
        });
        await _hub.Clients.All.SendAsync("assistantDone", new { turnId, text, targetDeviceId = device.DeviceId, audioCount = 1, engine = "local", model = "" });
    }

    private static readonly string[] Acks = ["Oi! Tô aqui.", "Pode falar.", "Sim?", "Tô ouvindo.", "Fala comigo."];
    private static string PickAcknowledgement() => Acks[Random.Shared.Next(Acks.Length)];

    private Task SendTranscriptAsync(DeviceInfo device, string text, bool accepted, string? reason) =>
        _hub.Clients.All.SendAsync("transcript", new { deviceId = device.DeviceId, deviceName = device.Name, text, accepted, reason });

    private Task SendIdleIfNoTurnAsync(string connectionId, DeviceInfo device) =>
        IsTurnActive ? Task.CompletedTask : _hub.Clients.Client(connectionId).SendAsync("state", new { state = "IDLE", detail = "", deviceId = device.DeviceId });

    /// <summary>Compara com o que a assistente está falando agora para descartar eco do alto-falante.</summary>
    private bool IsEcho(string heard)
    {
        string spoken;
        lock (_turnLock) spoken = _activeTurnSpokenText;
        if (spoken.Length == 0) return false;
        var heardWords = WakeWord.Normalize(heard).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (heardWords.Length == 0) return false;
        var spokenWords = WakeWord.Normalize(spoken).Split(' ').ToHashSet();
        var overlap = heardWords.Count(spokenWords.Contains) / (double)heardWords.Length;
        return overlap >= 0.6;
    }
}
