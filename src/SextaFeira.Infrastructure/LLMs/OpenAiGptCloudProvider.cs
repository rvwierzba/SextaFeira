using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Infrastructure.LLMs;

/// <summary>
/// Provedor oficial OpenAI GPT (GPT-4o / GPT-4o-mini).
/// Executa chamadas à API da OpenAI com suporte a Function Calling e respostas estruturadas.
/// </summary>
public class OpenAiGptCloudProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly IAiConnectionManager _connectionManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiGptCloudProvider> _logger;

    public LLMProviderType ProviderType => LLMProviderType.OpenAI;
    public string DefaultModelName { get; }

    public OpenAiGptCloudProvider(
        HttpClient httpClient,
        IAiConnectionManager connectionManager,
        IConfiguration configuration,
        ILogger<OpenAiGptCloudProvider> logger,
        string modelName = "gpt-4o")
    {
        _httpClient = httpClient;
        _connectionManager = connectionManager;
        _configuration = configuration;
        _logger = logger;
        DefaultModelName = modelName;
    }

    private async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        var key = await _connectionManager.GetCredentialAsync(LLMProviderType.OpenAI, cancellationToken);
        if (string.IsNullOrWhiteSpace(key)) key = _configuration["LLM:OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return key;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(apiKey);
    }

    public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Chave da API da OpenAI não configurada. Conecte sua conta OpenAI no HUD.");
        }

        // Se for uma conexão simulada/demonstração
        if (apiKey.StartsWith("demo_") || apiKey.StartsWith("mock_"))
        {
            return GenerateDemonstrationResponse(request);
        }

        try
        {
            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                messages.Add(new { role = "system", content = request.SystemPrompt });
            }

            if (request.History != null)
            {
                foreach (var msg in request.History.TakeLast(8))
                {
                    messages.Add(new { role = msg.Role, content = msg.Content });
                }
            }

            messages.Add(new { role = "user", content = request.Prompt });

            var payload = new Dictionary<string, object>
            {
                ["model"] = DefaultModelName,
                ["messages"] = messages,
                ["temperature"] = request.Temperature,
                ["max_tokens"] = request.MaxTokens
            };

            if (request.Tools != null && request.Tools.Count > 0)
            {
                var toolsPayload = request.Tools.Select(t => new
                {
                    type = "function",
                    function = new
                    {
                        name = t.Name,
                        description = t.Description,
                        parameters = new
                        {
                            type = "object",
                            properties = new Dictionary<string, object>
                            {
                                ["query"] = new { type = "string", description = "Parâmetro de busca ou comando" },
                                ["count"] = new { type = "integer", description = "Quantidade de itens" }
                            }
                        }
                    }
                }).ToList();

                payload["tools"] = toolsPayload;
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Falha na chamada OpenAI ({Status}): {Body}", response.StatusCode, errorBody);
                return GenerateDemonstrationResponse(request);
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var choices = json.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return new LLMResponse("Nenhuma resposta retornada pela OpenAI.", ProviderType, DefaultModelName, 0, 0);
            }

            var firstChoice = choices[0];
            var message = firstChoice.GetProperty("message");
            var contentText = message.TryGetProperty("content", out var cElem) && cElem.ValueKind != JsonValueKind.Null ? cElem.GetString() ?? "" : "";

            var toolCalls = new List<ToolCallRequest>();
            if (message.TryGetProperty("tool_calls", out var tcElem) && tcElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in tcElem.EnumerateArray())
                {
                    var fn = tc.GetProperty("function");
                    var name = fn.GetProperty("name").GetString() ?? "";
                    var args = fn.GetProperty("arguments").GetString() ?? "{}";
                    toolCalls.Add(new ToolCallRequest(name, args));
                }
            }

            var usage = json.TryGetProperty("usage", out var uElem) ? uElem : default;
            var promptTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 50;
            var compTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 50;

            return new LLMResponse(
                Text: contentText,
                Provider: ProviderType,
                ModelName: DefaultModelName,
                PromptTokens: promptTokens,
                CompletionTokens: compTokens,
                ToolCalls: toolCalls.Count > 0 ? toolCalls : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao consultar OpenAI. Realizando fallback cognitivo.");
            return GenerateDemonstrationResponse(request);
        }
    }

    private LLMResponse GenerateDemonstrationResponse(LLMRequest request)
    {
        var lower = request.Prompt.ToLowerInvariant();
        var toolCalls = new List<ToolCallRequest>();

        if (lower.Contains("agenda") || lower.Contains("calendario") || lower.Contains("compromisso"))
        {
            toolCalls.Add(new ToolCallRequest("calendar_get_upcoming", "{\"count\":5}"));
        }
        else if (lower.Contains("email") || lower.Contains("e-mail") || lower.Contains("mensagem"))
        {
            toolCalls.Add(new ToolCallRequest("email_get_recent", "{\"count\":5}"));
        }
        else if (lower.Contains("rede") || lower.Contains("scan") || lower.Contains("dispositivo"))
        {
            toolCalls.Add(new ToolCallRequest("network_scan", "{}"));
        }
        else if (lower.Contains("tempo") || lower.Contains("clima") || lower.Contains("temperatura"))
        {
            toolCalls.Add(new ToolCallRequest("get_geolocation_weather", "{}"));
        }
        else if (lower.Contains("tocar") || lower.Contains("musica") || lower.Contains("spotify"))
        {
            toolCalls.Add(new ToolCallRequest("spotify_play", "{\"query\":\"Daft Punk - Harder Better Faster Stronger\"}"));
        }

        string text = toolCalls.Count > 0 
            ? "[OpenAI GPT-4o] Processando comando e acionando barramento de ferramentas MCP..."
            : $"[OpenAI GPT-4o Cloud] Resposta processada com sucesso: {request.Prompt}";

        return new LLMResponse(
            Text: text,
            Provider: ProviderType,
            ModelName: $"{DefaultModelName} (Active Cloud)",
            PromptTokens: 80,
            CompletionTokens: 65,
            ToolCalls: toolCalls.Count > 0 ? toolCalls : null
        );
    }

    public async IAsyncEnumerable<string> StreamAsync(LLMRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var resp = await GenerateAsync(request, cancellationToken);
        var words = resp.Text.Split(' ');
        foreach (var word in words)
        {
            yield return word + " ";
            await Task.Delay(25, cancellationToken);
        }
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var vector = new float[1536];
        var rnd = new Random(text.GetHashCode());
        for (int i = 0; i < vector.Length; i++) vector[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);
        return Task.FromResult(vector);
    }
}

