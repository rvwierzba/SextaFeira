using SextaFeira.Domain.Enums;

namespace SextaFeira.Domain.Entities;

public class ConversationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Nova Sessão";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Role { get; set; } = "user"; // user, assistant, system, tool
    public string Content { get; set; } = string.Empty;
    public string? ToolCallsJson { get; set; }
    public string? AudioUrl { get; set; }
    public LLMProviderType? ProviderUsed { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class MemoryVector
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "Fact"; // Preference, Fact, Profile, Task, Memory
    public float[]? Embedding { get; set; }
    public double ImportanceScore { get; set; } = 1.0;
    public int AccessCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public bool IsRecycled { get; set; } = false;
}

public class NetworkDevice
{
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "Unknown";
    public string Vendor { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // Active, Idle, Offline
    public int LatencyMs { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}

public class CalendarEventItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = "Confirmed";
}

public class EmailMessageItem
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string PreviewSnippet { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public bool IsUnread { get; set; } = true;
}

public class OneDriveFileItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

public class CodeExecutionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Language { get; set; } = "python"; // python, csharp, nodejs, bash
    public string SourceCode { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Queued;
    public string? StdOut { get; set; }
    public string? StdErr { get; set; }
    public int ExitCode { get; set; }
    public TimeSpan Duration { get; set; }
    public int RetryAttempts { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SpotifyTrackItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public bool IsPlaying { get; set; }
    public string ContextUri { get; set; } = string.Empty;
}

public class SelfEvolutionResult
{
    public bool Succeeded { get; set; }
    public string TargetFile { get; set; } = string.Empty;
    public string FeatureDescription { get; set; } = string.Empty;
    public string ChangesSummary { get; set; } = string.Empty;
    public string BuildLog { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
