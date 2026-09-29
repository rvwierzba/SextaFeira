using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Application.Services;

public class MemoryRecyclerService
{
    private readonly IMemoryStore _memoryStore;
    private readonly ILLMProvider _summarizerProvider;
    private readonly ILogger<MemoryRecyclerService> _logger;

    public MemoryRecyclerService(
        IMemoryStore memoryStore,
        IEnumerable<ILLMProvider> llmProviders,
        ILogger<MemoryRecyclerService> logger)
    {
        _memoryStore = memoryStore;
        _summarizerProvider = llmProviders.First();
        _logger = logger;
    }

    public async Task<int> RunGarbageCollectionCycleAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando ciclo do Garbage Collector de Contexto & Reciclagem de Memória...");
        
        try
        {
            var recycledCount = await _memoryStore.RecycleContextGarbageCollectionAsync(olderThan, cancellationToken);
            _logger.LogInformation("Ciclo de reciclagem concluído. {Count} registros obsoletos consolidados e purgados.", recycledCount);
            return recycledCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a execução da reciclagem de memória.");
            return 0;
        }
    }
}
