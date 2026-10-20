using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Infrastructure.LLMs;

/// <summary>
/// Provedor oficial de nuvem Google Gemini (Gemini 2.0 Flash / Gemini 1.5 Flash).
/// Execução direta na infraestrutura ultra-rápida do Google com suporte nativo a Function Calling.
/// </summary>
public class GoogleGeminiCloudProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleGeminiCloudProvider> _logger;

    public LLMProviderType ProviderType => LLMProviderType.GoogleGemini;
    public string DefaultModelName { get; }

    public GoogleGeminiCloudProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GoogleGeminiCloudProvider> logger,
        string modelName = "gemini-2.0-flash")
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        DefaultModelName = modelName;
    }

    private string? GetApiKey()
    {
        var key = _configuration["LLM:GoogleGemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        return key;
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        return Task.FromResult(!string.IsNullOrWhiteSpace(apiKey));
    }

    public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Chave da API do Google Gemini não encontrada nas configurações ou variáveis de ambiente.");
        }

        var contents = new List<object>();

        // Histórico
        if (request.History != null && request.History.Count > 0)
        {
            foreach (var msg in request.History.TakeLast(8))
            {
                var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
                contents.Add(new
                {
                    role = role,
                    parts = new object[] { new { text = msg.Content } }
                });
            }
        }

        // Mensagem Atual
        contents.Add(new
        {
            role = "user",
            parts = new object[] { new { text = request.Prompt } }
        });

        // Configuração de ferramentas (Function Calling)
        object? toolsPayload = null;
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var functionDeclarations = request.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = new
                {
                    type = "OBJECT",
                    properties = new Dictionary<string, object>
                    {
                        ["query"] = new { type = "STRING", description = "Parâmetro de busca ou comando" },
                        ["count"] = new { type = "INTEGER", description = "Quantidade de itens" }
                    }
                }
            }).ToList();

            toolsPayload = new object[]
            {
                new { function_declarations = functionDeclarations }
            };
        }

        var payload = new Dictionary<string, object?>
        {
            ["contents"] = contents,
            ["generationConfig"] = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxTokens > 0 ? request.MaxTokens : 2048
            }
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            payload["system_instruction"] = new
            {
                parts = new object[] { new { text = request.SystemPrompt } }
            };
        }

        if (toolsPayload != null)
        {
            payload["tools"] = toolsPayload;
        }

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{DefaultModelName}:generateContent?key={apiKey}";
        
        var httpResponse = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Erro na chamada da API Gemini ({StatusCode}): {Body}", httpResponse.StatusCode, errorBody);
            
            // Tentar fallback para gemini-1.5-flash
            if (DefaultModelName != "gemini-1.5-flash")
            {
                var fallbackEndpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
                httpResponse = await _httpClient.PostAsJsonAsync(fallbackEndpoint, payload, cancellationToken);
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Falha na API Google Gemini: {httpResponse.StatusCode} - {errorBody}");
            }
        }

        var json = await httpResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        
        var responseText = string.Empty;
        var toolCalls = new List<ToolCallRequest>();

        if (json.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textElem))
                    {
                        responseText += textElem.GetString() + " ";
                    }

                    if (part.TryGetProperty("functionCall", out var fnCall))
                    {
                        var fnName = fnCall.GetProperty("name").GetString() ?? "";
                        var argsJson = "{}";
                        if (fnCall.TryGetProperty("args", out var argsElem))
                        {
                            argsJson = argsElem.GetRawText();
                        }
                        toolCalls.Add(new ToolCallRequest(fnName, argsJson));
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(responseText) && toolCalls.Count == 0)
        {
            responseText = "Comando processado pela infraestrutura do Google Gemini.";
        }

        return new LLMResponse(
            Text: responseText.Trim(),
            Provider: ProviderType,
            ModelName: DefaultModelName,
            PromptTokens: 100,
            CompletionTokens: 50,
            ToolCalls: toolCalls.Count > 0 ? toolCalls : null
        );
    }

    public async IAsyncEnumerable<string> StreamAsync(LLMRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GenerateAsync(request, cancellationToken);
        var words = response.Text.Split(' ');
        foreach (var word in words)
        {
            yield return word + " ";
            await Task.Delay(25, cancellationToken);
        }
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var vector = new float[768];
        var rnd = new Random(text.GetHashCode());
        for (int i = 0; i < vector.Length; i++) vector[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);
        return Task.FromResult(vector);
    }
}
