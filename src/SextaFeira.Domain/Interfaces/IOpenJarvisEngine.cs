using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Enums;

namespace SextaFeira.Domain.Interfaces;

/// <summary>
/// Contrato do motor OpenJarvis (Stanford Hazy Research / SAIL) integrado ao Sexta-Feira.
/// Orquestra os 5 primitivos: Intelligence, Engine, Agents, Tools/Memory e Learning/SpecSearch.
/// </summary>
public interface IOpenJarvisEngine
{
    /// <summary>
    /// Faz o probing do hardware local e calcula a quota de memória e inteligência recomendada.
    /// </summary>
    Task<HardwareQuota> ProbeHardwareAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o catálogo do Primitivo 1 (Intelligence).
    /// </summary>
    Task<List<IntelligenceSpec>> GetIntelligenceCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Execução local rápida com baixa latência (Primitivo 2: Engine + Primitivo 3: Agents).
    /// </summary>
    Task<LLMResponse> ExecuteLocalFastAsync(LLMRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Síntese de especificação guiada por modelo de fronteira (Primitivo 5: Spec Search).
    /// </summary>
    Task<SpecSearchPlan> SynthesizeSpecAsync(string userGoal, List<ToolDefinition> tools, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa uma instrução utilizando um plano compilado de Spec Search on-device.
    /// </summary>
    Task<LLMResponse> ExecuteSpecPlanAsync(SpecSearchPlan plan, LLMRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra telemetria de execução no ciclo de Learning & Traces.
    /// </summary>
    Task LogTraceAsync(OpenJarvisTrace trace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna os traces recentes para análise de eficiência e benchmark.
    /// </summary>
    Task<List<OpenJarvisTrace>> GetRecentTracesAsync(int count = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o status operacional geral e métricas agregadas do OpenJarvis.
    /// </summary>
    Task<OpenJarvisSystemStatus> GetSystemStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Alterna o modo de operação do OpenJarvis ("HybridSpecSearch", "LocalFastOnly", "CloudFrontier").
    /// </summary>
    Task SetActiveModeAsync(string mode, CancellationToken cancellationToken = default);
}

