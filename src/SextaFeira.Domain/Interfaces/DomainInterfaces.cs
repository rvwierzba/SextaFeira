using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Enums;

namespace SextaFeira.Domain.Interfaces;

public record LiveGeoLocation(
    double Latitude,
    double Longitude,
    string City,
    string Region,
    string Country,
    string FormattedAddress,
    string Source
);

public record LiveWeather(
    double TemperatureCelsius,
    string Condition,
    int HumidityPercent,
    double WindSpeedKmH,
    string WeatherCodeDescription,
    DateTime UpdatedAt
);

public record LiveGeoLocationAndWeather(
    LiveGeoLocation Location,
    LiveWeather Weather
);

public interface IGeolocationService
{
    Task<LiveGeoLocation> GetCurrentLocationAsync(double? clientLatitude = null, double? clientLongitude = null, CancellationToken cancellationToken = default);
    Task<LiveWeather> GetLiveWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    Task<LiveGeoLocationAndWeather> GetLiveLocationAndWeatherAsync(double? clientLatitude = null, double? clientLongitude = null, CancellationToken cancellationToken = default);
}

public record LLMRequest(
    string Prompt,
    string? SystemPrompt = null,
    List<ChatMessage>? History = null,
    double Temperature = 0.7,
    int MaxTokens = 2048,
    List<ToolDefinition>? Tools = null
);

public record LLMResponse(
    string Text,
    LLMProviderType Provider,
    string ModelName,
    int PromptTokens,
    int CompletionTokens,
    List<ToolCallRequest>? ToolCalls = null
);

public record ToolDefinition(
    string Name,
    string Description,
    string ParametersJsonSchema,
    ToolCategory Category
);

public record ToolCallRequest(
    string ToolName,
    string ArgumentsJson
);

public record ToolExecutionResult(
    string ToolName,
    bool Success,
    string Output,
    string? Error = null
);

public interface ILLMProvider
{
    LLMProviderType ProviderType { get; }
    string DefaultModelName { get; }
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamAsync(LLMRequest request, CancellationToken cancellationToken = default);
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}

public interface IMemoryStore
{
    Task StoreMemoryAsync(MemoryVector memory, CancellationToken cancellationToken = default);
    Task<List<MemoryVector>> SearchSimilarAsync(string query, int limit = 5, double minSimilarity = 0.6, CancellationToken cancellationToken = default);
    Task<int> RecycleContextGarbageCollectionAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
    Task<List<MemoryVector>> GetAllActiveMemoriesAsync(CancellationToken cancellationToken = default);
}

public interface ISandboxService
{
    Task<CodeExecutionJob> ExecuteCodeAsync(string language, string code, TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<CodeExecutionJob> RunSelfHealingLoopAsync(string language, string initialCode, string goalPrompt, int maxRetries = 3, CancellationToken cancellationToken = default);
}

public interface INetworkScanner
{
    Task<List<NetworkDevice>> ScanLocalNetworkAsync(CancellationToken cancellationToken = default);
    Task<NetworkDevice?> PingHostAsync(string ipOrHost, CancellationToken cancellationToken = default);
}

public interface IWebAutomationService
{
    Task<string> NavigateAndExtractMarkdownAsync(string url, CancellationToken cancellationToken = default);
    Task<byte[]> CaptureScreenshotAsync(string url, CancellationToken cancellationToken = default);
    Task<string> SearchWebAsync(string query, CancellationToken cancellationToken = default);
}

public interface IPersonalIntegrationsService
{
    Task<List<CalendarEventItem>> GetUpcomingEventsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<CalendarEventItem> CreateCalendarEventAsync(CalendarEventItem eventItem, CancellationToken cancellationToken = default);
    Task<List<EmailMessageItem>> GetRecentEmailsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<List<OneDriveFileItem>> GetOneDriveFilesAsync(string folderPath = "/", CancellationToken cancellationToken = default);
}

public interface IVoiceEngine
{
    Task<string> TranscribeAudioAsync(byte[] audioData, string language = "pt-BR", CancellationToken cancellationToken = default);
    Task<byte[]> SynthesizeSpeechAsync(string text, string voice = "pt-BR-FranciscaNeural", CancellationToken cancellationToken = default);
}

public interface IMcpHost
{
    Task<List<ToolDefinition>> GetAvailableToolsAsync(CancellationToken cancellationToken = default);
    Task<ToolExecutionResult> DispatchToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default);
}
