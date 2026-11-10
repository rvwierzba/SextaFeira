using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Agent;
using SextaFeira.Infrastructure.Storage;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace SextaFeira.Infrastructure.Voice;

/// <summary>
/// Reconhecimento de fala 100% local com Whisper (whisper.cpp via Whisper.net), acelerado por GPU (Vulkan) quando possível.
/// Recebe áudio de qualquer dispositivo da rede, então funciona igual no PC, no celular ou no tablet.
/// </summary>
public sealed class WhisperSpeechToText : ISpeechToText, IHostedService, IDisposable
{
    // Frases que o Whisper costuma "alucinar" em silêncio ou ruído.
    private static readonly string[] Hallucinations =
    [
        "legendas pela comunidade amara.org", "obrigado por assistir", "inscreva-se", "legendado por", "transcrição por",
        "e aí, pessoal", "tchau, tchau", "música", "[música]", "(música)", "♪", "...", "obrigado.", "obrigada."
    ];

    private readonly ISettingsStore _settings;
    private readonly ILogger<WhisperSpeechToText> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private WhisperFactory? _factory;
    private string? _loadedModel;

    public WhisperSpeechToText(ISettingsStore settings, ILogger<WhisperSpeechToText> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsReady => _factory != null && _loadedModel != null;
    public string Status { get; private set; } = "carregando";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try { await EnsureLoadedAsync(CancellationToken.None); }
            catch (Exception ex)
            {
                Status = $"erro: {ex.Message}";
                _logger.LogError(ex, "Whisper não carregou.");
            }
        }, CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<byte> pcm16Mono16k, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        var samples = new float[pcm16Mono16k.Length / 2];
        var span = pcm16Mono16k.Span;
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (short)(span[2 * i] | (span[2 * i + 1] << 8)) / 32768f;

        var sw = Stopwatch.StartNew();
        var text = new StringBuilder();
        double maxNoSpeech = 0;
        await _lock.WaitAsync(ct);
        try
        {
            // O Whisper codifica sempre uma janela fixa; para frases curtas, uma janela menor é bem mais rápida.
            var processor = ProcessorFor(samples.Length / 16000.0);
            await foreach (var segment in processor.ProcessAsync(samples, ct))
            {
                text.Append(segment.Text);
                maxNoSpeech = Math.Max(maxNoSpeech, segment.NoSpeechProbability);
            }
        }
        finally { _lock.Release(); }
        sw.Stop();

        var result = text.ToString().Trim();
        if (IsHallucination(result)) result = "";
        return new TranscriptionResult(result, maxNoSpeech, sw.Elapsed);
    }

    /// <summary>Faixas de contexto de áudio (50 quadros por segundo): ~7 s, ~15 s e 30 s.</summary>
    private static readonly (double MaxSeconds, int AudioContext)[] Buckets = [(7.0, 384), (14.5, 768), (double.MaxValue, 1500)];
    private readonly Dictionary<int, WhisperProcessor> _processors = new();

    private WhisperProcessor ProcessorFor(double seconds)
    {
        var ctx = Buckets.First(b => seconds <= b.MaxSeconds).AudioContext;
        if (_processors.TryGetValue(ctx, out var p)) return p;
        var builder = _factory!.CreateBuilder()
            .WithLanguage("pt")
            .WithPrompt("Sexta-Feira, assistente pessoal. Conversa em português do Brasil.")
            .WithNoContext()
            .WithSingleSegment()
            .WithThreads(Math.Max(2, Environment.ProcessorCount / 2));
        if (ctx < 1500) builder = builder.WithAudioContextSize(ctx);
        p = builder.Build();
        _processors[ctx] = p;
        return p;
    }

    private static bool IsHallucination(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        if (t.Length == 0) return true;
        return Hallucinations.Any(h => t == h || (h.Length > 12 && t.Contains(h)));
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        var model = _settings.Current.Voice.WhisperModel;
        if (_factory != null && _loadedModel == model) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_factory != null && _loadedModel == model) return;
            foreach (var p in _processors.Values) p.Dispose();
            _processors.Clear();
            _factory?.Dispose();

            var (type, quant, file) = ResolveModel(model);
            var path = Path.Combine(DataPaths.Models, file);
            if (!File.Exists(path))
            {
                Status = $"baixando modelo Whisper '{model}' (só na primeira vez)...";
                _logger.LogInformation("Baixando modelo Whisper {Model} para {Path}", model, path);
                await using var src = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(type, quant, ct);
                var tmp = path + ".part";
                await using (var dst = File.Create(tmp)) await src.CopyToAsync(dst, ct);
                File.Move(tmp, path, overwrite: true);
            }

            Status = "carregando modelo na GPU...";
            RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];
            _factory = WhisperFactory.FromPath(path);
            ProcessorFor(5); // pré-aquece a faixa mais usada
            _loadedModel = model;
            Status = $"pronto ({model}, {RuntimeOptions.LoadedLibrary?.ToString() ?? "cpu"})";
            _logger.LogInformation("Whisper pronto: {Status}", Status);
        }
        finally { _lock.Release(); }
    }

    private static (GgmlType Type, QuantizationType Quant, string File) ResolveModel(string name) => name.ToLowerInvariant() switch
    {
        "tiny" => (GgmlType.Tiny, QuantizationType.Q5_1, "ggml-tiny-q5_1.bin"),
        "base" => (GgmlType.Base, QuantizationType.Q5_1, "ggml-base-q5_1.bin"),
        "medium" => (GgmlType.Medium, QuantizationType.Q5_0, "ggml-medium-q5_0.bin"),
        "large-v3-turbo" or "turbo" => (GgmlType.LargeV3Turbo, QuantizationType.Q5_0, "ggml-large-v3-turbo-q5_0.bin"),
        _ => (GgmlType.Small, QuantizationType.Q5_1, "ggml-small-q5_1.bin"),
    };

    public void Dispose()
    {
        foreach (var p in _processors.Values) p.Dispose();
        _factory?.Dispose();
    }
}
