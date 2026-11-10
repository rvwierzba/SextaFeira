using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Infrastructure.Voice;

public class VoiceEngineService : IVoiceEngine
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VoiceEngineService> _logger;
    private readonly IAiConnectionManager? _aiConnectionManager;

    public VoiceEngineService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<VoiceEngineService> logger,
        IAiConnectionManager? aiConnectionManager = null)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _aiConnectionManager = aiConnectionManager;
    }

    private string? GetGeminiApiKey()
    {
        var key = _aiConnectionManager?.GetCredentialAsync(LLMProviderType.GoogleGemini).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["LLM:GoogleGemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        return key;
    }

    private string? GetOpenAiApiKey()
    {
        var key = _aiConnectionManager?.GetCredentialAsync(LLMProviderType.OpenAI).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["LLM:OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return key;
    }

    private string? GetGroqApiKey()
    {
        var key = _configuration["LLM:Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["GROQ_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        return key;
    }

    public async Task<string> TranscribeAudioAsync(byte[] audioData, string language = "pt-BR", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processando áudio ({Size} bytes) via pipeline de STT...", audioData.Length);

        if (audioData == null || audioData.Length < 100)
        {
            return string.Empty;
        }

        // 1. Tentar transcrição via Google Gemini Multimodal Audio (Ultra Rápido)
        var geminiKey = GetGeminiApiKey();
        if (!string.IsNullOrWhiteSpace(geminiKey))
        {
            try
            {
                var transcript = await TranscribeWithGeminiAsync(audioData, geminiKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    _logger.LogInformation("Transcrição bem-sucedida via Gemini: {Transcript}", transcript);
                    return transcript;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha na transcrição via Gemini. Tentando provedor alternativo...");
            }
        }

        // 2. Tentar transcrição via Groq Whisper (Latência sub-200ms)
        var groqKey = GetGroqApiKey();
        if (!string.IsNullOrWhiteSpace(groqKey))
        {
            try
            {
                var transcript = await TranscribeWithWhisperEndpointAsync(
                    "https://api.groq.com/openai/v1/audio/transcriptions",
                    groqKey,
                    "whisper-large-v3",
                    audioData,
                    cancellationToken
                );
                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    _logger.LogInformation("Transcrição bem-sucedida via Groq Whisper: {Transcript}", transcript);
                    return transcript;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha na transcrição via Groq Whisper...");
            }
        }

        // 3. Tentar transcrição via OpenAI Whisper
        var openAiKey = GetOpenAiApiKey();
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            try
            {
                var transcript = await TranscribeWithWhisperEndpointAsync(
                    "https://api.openai.com/v1/audio/transcriptions",
                    openAiKey,
                    "whisper-1",
                    audioData,
                    cancellationToken
                );
                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    _logger.LogInformation("Transcrição bem-sucedida via OpenAI Whisper: {Transcript}", transcript);
                    return transcript;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha na transcrição via OpenAI Whisper...");
            }
        }

        // Fallback autônomo imediato caso nenhuma chave externa esteja conectada
        _logger.LogInformation("Nenhuma chave de API de STT ativa. Áudio não decodificado.");
        return string.Empty;
    }

    private async Task<string> TranscribeWithGeminiAsync(byte[] audioData, string apiKey, CancellationToken cancellationToken)
    {
        var base64Audio = Convert.ToBase64String(audioData);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = "audio/webm",
                                data = base64Audio
                            }
                        },
                        new
                        {
                            text = "Transcreva estritamente as palavras ditas pelo usuário neste áudio em português do Brasil. Retorne unicamente o texto transcrito, sem introdução, sem aspas e sem explicações."
                        }
                    }
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = jsonDoc.RootElement;
        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                var text = parts[0].GetProperty("text").GetString();
                return text?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private async Task<string> TranscribeWithWhisperEndpointAsync(
        string endpoint,
        string apiKey,
        string modelName,
        byte[] audioData,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var content = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(audioData);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        content.Add(audioContent, "file", "recording.webm");
        content.Add(new StringContent(modelName), "model");
        content.Add(new StringContent("pt"), "language");

        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (jsonDoc.RootElement.TryGetProperty("text", out var textProp))
        {
            return textProp.GetString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    public async Task<byte[]> SynthesizeSpeechAsync(string text, string voice = "pt-BR", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sintetizando fala PT-BR para: {Text}", text.Length > 60 ? text.Substring(0, 60) + "..." : text);

        try
        {
            var cleanText = Uri.EscapeDataString(text.Trim());
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={cleanText}&tl=pt-BR&client=tw-ob";
            using var msg = new HttpRequestMessage(HttpMethod.Get, url);
            msg.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            var resp = await _httpClient.SendAsync(msg, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                return await resp.Content.ReadAsByteArrayAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na síntese de áudio PT-BR via engine de voz.");
        }

        return Array.Empty<byte>();
    }
}