namespace SextaFeira.Domain.Enums;

public enum AgentState
{
    Idle = 0,
    Listening = 1,
    Thinking = 2,
    Speaking = 3,
    Executing = 4
}

public enum LLMProviderType
{
    OllamaLocal = 0,
    Phi3LocalOnnx = 1,
    OpenAI = 2,
    Anthropic = 3,
    GoogleGemini = 4,
    Groq = 5
}

public enum ExecutionStatus
{
    Queued = 0,
    Running = 1,
    Success = 2,
    Failed = 3,
    SelfHealing = 4
}

public enum ToolCategory
{
    System = 0,
    Network = 1,
    StealthBrowser = 2,
    Sandbox = 3,
    Calendar = 4,
    Email = 5,
    Files = 6,
    Geolocation = 7
}
