using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Agent;

namespace SextaFeira.Infrastructure.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<JsonSettingsStore> _logger;

    private readonly IConfiguration? _config;

    public JsonSettingsStore(ILogger<JsonSettingsStore> logger, IConfiguration? config = null)
    {
        _logger = logger;
        _config = config;
        Current = ApplyEnvironmentOverrides(Load());
    }

    /// <summary>
    /// Variáveis de ambiente / appsettings vencem o settings.json (útil em Docker e servidores):
    /// Ollama__BaseUrl, Ollama__Model, SelfCode__Enabled, Voice__WhisperModel, ActiveEngine.
    /// </summary>
    private AssistantSettings ApplyEnvironmentOverrides(AssistantSettings s)
    {
        if (_config == null) return s;
        if (_config["Ollama:BaseUrl"] is { Length: > 0 } url) s.Ollama.BaseUrl = url;
        if (_config["Ollama:Model"] is { Length: > 0 } model) s.Ollama.Model = model;
        if (_config["Voice:WhisperModel"] is { Length: > 0 } whisper) s.Voice.WhisperModel = whisper;
        if (_config["ActiveEngine"] is { Length: > 0 } engine) s.ActiveEngine = engine;
        if (bool.TryParse(_config["SelfCode:Enabled"], out var selfCode)) s.SelfCode.Enabled = selfCode;
        return s;
    }

    public AssistantSettings Current { get; private set; }
    public event Action<AssistantSettings>? Changed;

    public async Task SaveAsync(AssistantSettings settings, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var tmp = DataPaths.Settings + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(settings, Options), ct);
            File.Move(tmp, DataPaths.Settings, overwrite: true);
            Current = settings;
        }
        finally { _lock.Release(); }
        Changed?.Invoke(settings);
    }

    private AssistantSettings Load()
    {
        try
        {
            if (File.Exists(DataPaths.Settings))
                return JsonSerializer.Deserialize<AssistantSettings>(File.ReadAllText(DataPaths.Settings), Options) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "settings.json inválido; usando padrões (o arquivo antigo foi mantido como .bak).");
            try { File.Copy(DataPaths.Settings, DataPaths.Settings + ".bak", true); } catch { }
        }

        var defaults = new AssistantSettings();
        try { File.WriteAllText(DataPaths.Settings, JsonSerializer.Serialize(defaults, Options)); } catch { }
        return defaults;
    }

    /// <summary>Cópia profunda (via JSON) para edição segura.</summary>
    public static AssistantSettings Clone(AssistantSettings s) =>
        JsonSerializer.Deserialize<AssistantSettings>(JsonSerializer.Serialize(s, Options), Options)!;
}
