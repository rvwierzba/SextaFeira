using SextaFeira.Domain.Enums;

namespace SextaFeira.Domain.Entities;

public class AiAccountConnection
{
    public LLMProviderType Provider { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsActive { get; set; }
    public int LatencyMs { get; set; } = 0;
    public string AuthType { get; set; } = "QuickConnect"; // Google_OAuth, OpenAI_Direct, Anthropic_Direct, QuickConnect
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}

