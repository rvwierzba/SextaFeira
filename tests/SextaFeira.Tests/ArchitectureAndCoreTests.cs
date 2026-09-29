using Microsoft.Extensions.Logging.Abstractions;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;
using SextaFeira.Infrastructure.Geolocation;
using SextaFeira.Infrastructure.LLMs;
using SextaFeira.Infrastructure.Persistence;
using Xunit;

namespace SextaFeira.Tests;

public class ArchitectureAndCoreTests
{
    [Fact]
    public void AesEncryptionVault_ShouldEncryptAndDecryptAccurately()
    {
        // Arrange
        var vault = new AesEncryptionVault("SextaFeira-Super-Secret-Key-2026");
        var originalSecret = "openai_api_key_sk_test_1234567890abcdef";

        // Act
        var (cipherText, ivBase64) = vault.Encrypt(originalSecret);
        var decrypted = vault.Decrypt(cipherText, ivBase64);

        // Assert
        Assert.NotEmpty(cipherText);
        Assert.NotEmpty(ivBase64);
        Assert.Equal(originalSecret, decrypted);
    }

    [Fact]
    public async Task OllamaFallbackProvider_ShouldProvideAutonomousLocalResponse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var logger = NullLogger<OllamaLocalFallbackProvider>.Instance;
        var provider = new OllamaLocalFallbackProvider(httpClient, config, logger);

        // Act
        var isHealthy = await provider.IsHealthyAsync();
        var response = await provider.GenerateAsync(new LLMRequest("Sexta-Feira, apresentar status do sistema"));

        // Assert
        Assert.True(isHealthy);
        Assert.NotNull(response);
        Assert.Equal(LLMProviderType.OllamaLocal, response.Provider);
        Assert.Contains("Sexta-Feira", response.Text);
    }

    [Fact]
    public async Task LiveGeolocationService_ShouldRetrieveDynamicRealTimeLocationAndGenerateEmbedding()
    {
        // Arrange
        var httpClient = new HttpClient();
        var geoLogger = NullLogger<LiveGeolocationService>.Instance;
        var geoService = new LiveGeolocationService(httpClient, geoLogger);

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var llmLogger = NullLogger<OllamaLocalFallbackProvider>.Instance;
        var provider = new OllamaLocalFallbackProvider(httpClient, config, llmLogger);

        // Act - Obtenção dinâmica em tempo real (GPS / IP / Reverse Geocoding)
        var liveGeoWeather = await geoService.GetLiveLocationAndWeatherAsync();
        
        Assert.NotNull(liveGeoWeather);
        Assert.NotNull(liveGeoWeather.Location);
        Assert.NotEmpty(liveGeoWeather.Location.FormattedAddress);
        Assert.NotNull(liveGeoWeather.Weather);

        // Geração do vetor de embedding a partir da localização em tempo real
        var dynamicLocationText = $"{liveGeoWeather.Location.FormattedAddress} (Lat: {liveGeoWeather.Location.Latitude}, Lon: {liveGeoWeather.Location.Longitude})";
        var vector = await provider.GenerateEmbeddingAsync(dynamicLocationText);

        // Assert
        Assert.NotNull(vector);
        Assert.Equal(384, vector.Length);
        
        // Verifica normalização do vetor (magnitude ~ 1.0)
        var magnitude = Math.Sqrt(vector.Sum(x => x * x));
        Assert.InRange(magnitude, 0.99, 1.01);
    }
}
