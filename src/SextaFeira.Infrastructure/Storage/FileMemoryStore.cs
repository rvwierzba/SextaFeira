using System.Text.Json;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Agent;
using SextaFeira.Infrastructure.Engines;

namespace SextaFeira.Infrastructure.Storage;

/// <summary>
/// Memória de longo prazo em arquivo (data/memory.json) com busca semântica por embeddings locais do Ollama.
/// Sem banco de dados: funciona em qualquer máquina. Se o modelo de embedding não existir, cai para busca por palavras.
/// </summary>
public sealed class FileMemoryStore : IMemoryStore
{
    private sealed record Entry(string Id, string Text, DateTime CreatedAt, float[]? Vector);

    private readonly OllamaEngine _ollama;
    private readonly ILogger<FileMemoryStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Entry>? _entries;

    public FileMemoryStore(OllamaEngine ollama, ILogger<FileMemoryStore> logger)
    {
        _ollama = ollama;
        _logger = logger;
    }

    public async Task<MemoryItem> SaveAsync(string text, CancellationToken ct = default)
    {
        var vector = await TryEmbedAsync(text, ct);
        var entry = new Entry(Guid.NewGuid().ToString("N")[..10], text.Trim(), DateTime.UtcNow, vector);
        await _lock.WaitAsync(ct);
        try
        {
            var entries = await LoadAsync(ct);
            entries.Add(entry);
            await PersistAsync(entries, ct);
        }
        finally { _lock.Release(); }
        return new MemoryItem(entry.Id, entry.Text, entry.CreatedAt);
    }

    public async Task<IReadOnlyList<MemoryItem>> SearchAsync(string query, int limit = 4, CancellationToken ct = default)
    {
        List<Entry> entries;
        await _lock.WaitAsync(ct);
        try { entries = (await LoadAsync(ct)).ToList(); }
        finally { _lock.Release(); }
        if (entries.Count == 0) return [];

        var qv = await TryEmbedAsync(query, ct);
        IEnumerable<(Entry E, double Score)> scored;
        if (qv != null && entries.Any(e => e.Vector != null))
        {
            scored = entries.Where(e => e.Vector != null).Select(e => (e, Cosine(qv, e.Vector!))).Where(x => x.Item2 > 0.55);
        }
        else
        {
            var words = Tokens(query);
            scored = entries.Select(e => (e, (double)Tokens(e.Text).Intersect(words).Count())).Where(x => x.Item2 > 0);
        }

        return scored.OrderByDescending(x => x.Score).Take(limit)
            .Select(x => new MemoryItem(x.E.Id, x.E.Text, x.E.CreatedAt, Math.Round(x.Score, 3))).ToList();
    }

    public async Task<IReadOnlyList<MemoryItem>> ListAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try { return (await LoadAsync(ct)).Select(e => new MemoryItem(e.Id, e.Text, e.CreatedAt)).ToList(); }
        finally { _lock.Release(); }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var entries = await LoadAsync(ct);
            var removed = entries.RemoveAll(e => e.Id == id) > 0;
            if (removed) await PersistAsync(entries, ct);
            return removed;
        }
        finally { _lock.Release(); }
    }

    private async Task<float[]?> TryEmbedAsync(string text, CancellationToken ct)
    {
        try { return await _ollama.EmbedAsync(text, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Embedding indisponível; usando busca por palavras.");
            return null;
        }
    }

    private async Task<List<Entry>> LoadAsync(CancellationToken ct)
    {
        if (_entries != null) return _entries;
        if (File.Exists(DataPaths.Memory))
        {
            try
            {
                await using var fs = File.OpenRead(DataPaths.Memory);
                _entries = await JsonSerializer.DeserializeAsync<List<Entry>>(fs, cancellationToken: ct) ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "memory.json corrompido; começando vazio.");
                _entries = [];
            }
        }
        return _entries ??= [];
    }

    private static async Task PersistAsync(List<Entry> entries, CancellationToken ct)
    {
        var tmp = DataPaths.Memory + ".tmp";
        await using (var fs = File.Create(tmp))
            await JsonSerializer.SerializeAsync(fs, entries, cancellationToken: ct);
        File.Move(tmp, DataPaths.Memory, overwrite: true);
    }

    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static HashSet<string> Tokens(string s) =>
        s.ToLowerInvariant().Split([' ', ',', '.', '!', '?', ';', ':'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3).ToHashSet();
}
