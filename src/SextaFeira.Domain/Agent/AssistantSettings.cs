namespace SextaFeira.Domain.Agent;

/// <summary>
/// Configuração viva da assistente, persistida em data/settings.json e editável pelo HUD.
/// </summary>
public sealed class AssistantSettings
{
    public string AssistantName { get; set; } = "Sexta-Feira";
    public string UserName { get; set; } = "";

    /// <summary>Engine ativa: "ollama" (padrão, local), "openai", "anthropic" ou "gemini".</summary>
    public string ActiveEngine { get; set; } = "ollama";

    /// <summary>Se a engine em nuvem falhar, responde com o modelo local.</summary>
    public bool FallbackToLocal { get; set; } = true;

    public OllamaSettings Ollama { get; set; } = new();
    public CloudEngineSettings OpenAI { get; set; } = new() { Model = "gpt-4o-mini", BaseUrl = "https://api.openai.com/v1" };
    public CloudEngineSettings Anthropic { get; set; } = new() { Model = "claude-opus-5-5", BaseUrl = "https://api.anthropic.com" };
    public CloudEngineSettings Gemini { get; set; } = new() { Model = "gemini-2.5-flash", BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai" };

    /// <summary>Opcional: URL de um SearXNG próprio (ex.: http://localhost:8080) para busca web completa sem chave.</summary>
    public string SearxngUrl { get; set; } = "";

    public VoiceSettings Voice { get; set; } = new();
    public SelfCodeSettings SelfCode { get; set; } = new();
}

public sealed class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    /// <summary>Modelo de chat. Precisa suportar tool calling (llama3.1, qwen2.5, qwen3, mistral-nemo...).</summary>
    public string Model { get; set; } = "llama3.1:8b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int ContextLength { get; set; } = 6144;
    /// <summary>Mantém o modelo carregado na GPU entre turnos (evita 5-10 s de recarga).</summary>
    public string KeepAlive { get; set; } = "30m";
}

public sealed class CloudEngineSettings
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string BaseUrl { get; set; } = "";
}

public sealed class VoiceSettings
{
    /// <summary>
    /// Parte do nome da voz preferida em cada dispositivo (vozes do sistema/navegador, em ordem de preferência).
    /// Edge: "Francisca Online (Natural)" / "Thalita Online"; Chrome: "Google português do Brasil"; iOS/macOS: "Luciana".
    /// </summary>
    public List<string> PreferredVoices { get; set; } = ["Francisca", "Thalita", "Google português do Brasil", "Luciana", "Maria"];
    /// <summary>Velocidade da fala (1.0 = normal).</summary>
    public double SpeechRate { get; set; } = 1.1;

    /// <summary>Modelo Whisper local: tiny, base, small, medium, large-v3-turbo.</summary>
    public string WhisperModel { get; set; } = "small";

    public List<string> WakeWords { get; set; } = ["sexta-feira", "sexta feira", "sexta", "sextafeira", "friday"];

    /// <summary>Exige a palavra de ativação para iniciar uma conversa.</summary>
    public bool RequireWakeWord { get; set; } = true;

    /// <summary>Depois de uma resposta, por quantos segundos dá para continuar falando sem repetir a palavra de ativação.</summary>
    public int FollowUpWindowSeconds { get; set; } = 25;
}

public sealed class SelfCodeSettings
{
    /// <summary>Permite que a assistente leia/edite o próprio código-fonte.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Raiz do repositório. Vazio = detecta automaticamente (pasta com SextaFeira.sln).</summary>
    public string RepoPath { get; set; } = "";
}

public interface ISettingsStore
{
    AssistantSettings Current { get; }
    Task SaveAsync(AssistantSettings settings, CancellationToken ct = default);
    event Action<AssistantSettings>? Changed;
}
