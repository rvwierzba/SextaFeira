using System.Text.Json;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Agent;

namespace SextaFeira.Infrastructure.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<JsonSettingsStore> _logger;

    public JsonSettingsStore(ILogger<JsonSettingsStore> logger)
    {
        _logger = logger;
        Current = Load();
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
