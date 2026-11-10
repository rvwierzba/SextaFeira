namespace SextaFeira.Domain.Entities;

/// <summary>
/// Quota e detecção de hardware local baseada na filosofia "Intelligence Per Watt" de Stanford.
/// </summary>
public record HardwareQuota(
    string AcceleratorType, // CUDA, Apple_Metal, Vulkan, DirectML, CPU
    int ProcessorCount,
    long TotalMemoryMb,
    long AvailableMemoryMb,
    long DedicatedVramMb,
    string RecommendedLocalModel,
    string RecommendedQuantization,
    double PowerEfficiencyIndex
);

/// <summary>
/// Especificação do modelo (Primitivo 1: Intelligence).
/// </summary>
public record IntelligenceSpec(
    string ModelName,
    string Quantization,
    string ParameterCount,
    int ContextWindowTokens,
    long RequiredVramMb,
    double TargetLatencyMs,
    bool IsOnDeviceReady
);

/// <summary>
/// Plano de Execução Compilado via LLM-Guided Spec Search (Primitivo 5: Learning & Spec Search).
/// </summary>
public class SpecSearchPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IntentHash { get; set; } = string.Empty;
    public string UserGoal { get; set; } = string.Empty;
    public string SynthesizedBy { get; set; } = "GoogleGemini-2.0-Flash";
    public List<string> ExecutionSteps { get; set; } = new();
    public List<string> RequiredTools { get; set; } = new();
    public string CompiledLocalPromptTemplate { get; set; } = string.Empty;
    public bool PassedRegressionGate { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int UsageCount { get; set; } = 1;
}

/// <summary>
/// Registro de telemetria de execução para ciclo fechado de aprendizado (Primitivo 5: Learning & Traces).
/// </summary>
public record OpenJarvisTrace(
    Guid Id,
    DateTime Timestamp,
    string InputPrompt,
    string ExecutionRoute, // "LocalFastImmediate", "HybridSpecSearchExecution", "CloudFallback"
    string ModelUsed,
    double LatencyMs,
    double TimeToFirstTokenMs,
    double TokensPerSecond,
    double IntelligencePerWattScore,
    bool Success,
    string Summary
);

/// <summary>
/// Estado agregado do subsistema OpenJarvis.
/// </summary>
public record OpenJarvisSystemStatus(
    bool IsLocalEngineOnline,
    string ActiveMode, // "HybridSpecSearch", "LocalFastOnly", "CloudFrontier"
    HardwareQuota Hardware,
    IntelligenceSpec ActiveIntelligence,
    int CachedSpecsCount,
    double AverageLatencyMs,
    double IntelligencePerWattIndex,
    List<OpenJarvisTrace> RecentTraces
);

