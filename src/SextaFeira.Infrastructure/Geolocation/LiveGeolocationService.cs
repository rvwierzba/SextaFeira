using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Infrastructure.Geolocation;

public class LiveGeolocationService : IGeolocationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LiveGeolocationService> _logger;

    private static LiveGeoLocation? _lastKnownLocation;

    public LiveGeolocationService(HttpClient httpClient, ILogger<LiveGeolocationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SextaFeira-Autonomous-AI/1.0 (Spatial-Radar)");
        }
    }

    public async Task<LiveGeoLocation> GetCurrentLocationAsync(
        double? clientLatitude = null, 
        double? clientLongitude = null, 
        CancellationToken cancellationToken = default)
    {
        // 1. Se o cliente (PWA / Browser / Dispositivo) enviou coordenadas de GPS nativas precisas
        if (clientLatitude.HasValue && clientLongitude.HasValue)
        {
            var lat = clientLatitude.Value;
            var lon = clientLongitude.Value;

            try
            {
                // Reverse Geocoding via OpenStreetMap Nominatim (Gratuito / Sem chaves)
                var reverseUrl = $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&format=json";
                var reverseResp = await _httpClient.GetFromJsonAsync<JsonElement>(reverseUrl, cancellationToken);
                
                var address = reverseResp.TryGetProperty("address", out var addr) ? addr : default;
                var city = addr.TryGetProperty("city", out var c) ? c.GetString() :
                           addr.TryGetProperty("town", out var t) ? t.GetString() :
                           addr.TryGetProperty("municipality", out var m) ? m.GetString() : "Localização Detectada";
                
                var state = addr.TryGetProperty("state", out var s) ? s.GetString() : "SP";
                var country = addr.TryGetProperty("country", out var co) ? co.GetString() : "Brasil";
                var displayName = reverseResp.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? $"{city}, {state}" : $"{city}, {state}";

                var loc = new LiveGeoLocation(
                    Latitude: lat,
                    Longitude: lon,
                    City: city ?? "Piracaia",
                    Region: state ?? "SP",
                    Country: country ?? "Brasil",
                    FormattedAddress: displayName,
                    Source: "GPS Nativo do Dispositivo / PWA"
                );

                _lastKnownLocation = loc;
                return loc;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reverse geocoding falhou, utilizando coordenadas de GPS puras.");
                return new LiveGeoLocation(lat, lon, "GPS Coordenadas", "Detectado", "Brasil", $"{lat:F4}, {lon:F4}", "GPS Nativo");
            }
        }

        // 2. Consulta de IP Geolocation em tempo real via IP-API (Gratuito / Baixa Latência)
        try
        {
            var ipResp = await _httpClient.GetFromJsonAsync<JsonElement>("http://ip-api.com/json/?fields=status,country,regionName,city,lat,lon,query", cancellationToken);
            if (ipResp.TryGetProperty("status", out var status) && status.GetString() == "success")
            {
                var lat = ipResp.GetProperty("lat").GetDouble();
                var lon = ipResp.GetProperty("lon").GetDouble();
                var city = ipResp.GetProperty("city").GetString() ?? "São Paulo";
                var region = ipResp.GetProperty("regionName").GetString() ?? "São Paulo";
                var country = ipResp.GetProperty("country").GetString() ?? "Brasil";

                var loc = new LiveGeoLocation(
                    Latitude: lat,
                    Longitude: lon,
                    City: city,
                    Region: region,
                    Country: country,
                    FormattedAddress: $"{city}, {region} - {country}",
                    Source: "IP Geolocation Tempo Real"
                );

                _lastKnownLocation = loc;
                return loc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na detecção de IP Geolocation em tempo real. Usando cache ou fallback.");
        }

        // 3. Fallback de contingência
        return _lastKnownLocation ?? new LiveGeoLocation(
            Latitude: -23.0544,
            Longitude: -46.3589,
            City: "Piracaia",
            Region: "São Paulo",
            Country: "Brasil",
            FormattedAddress: "Piracaia, São Paulo, Brasil",
            Source: "Sexta-Feira Spatial Base"
        );
    }

    public async Task<LiveWeather> GetLiveWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        try
        {
            var latStr = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lonStr = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m";

            var resp = await _httpClient.GetFromJsonAsync<JsonElement>(weatherUrl, cancellationToken);
            if (resp.TryGetProperty("current", out var current))
            {
                var temp = current.GetProperty("temperature_2m").GetDouble();
                var humidity = current.GetProperty("relative_humidity_2m").GetInt32();
                var wind = current.GetProperty("wind_speed_10m").GetDouble();
                var weatherCode = current.GetProperty("weather_code").GetInt32();

                var (condition, desc) = DecodeWmoWeatherCode(weatherCode);

                return new LiveWeather(
                    TemperatureCelsius: temp,
                    Condition: condition,
                    HumidityPercent: humidity,
                    WindSpeedKmH: wind,
                    WeatherCodeDescription: desc,
                    UpdatedAt: DateTime.UtcNow
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao consultar Open-Meteo em tempo real. Retornando telemetria aproximada.");
        }

        return new LiveWeather(
            TemperatureCelsius: 24.0,
            Condition: "Ensolarado / Parcialmente Nublado",
            HumidityPercent: 60,
            WindSpeedKmH: 10.5,
            WeatherCodeDescription: "Condições meteorológicas estáveis",
            UpdatedAt: DateTime.UtcNow
        );
    }

    public async Task<LiveGeoLocationAndWeather> GetLiveLocationAndWeatherAsync(
        double? clientLatitude = null, 
        double? clientLongitude = null, 
        CancellationToken cancellationToken = default)
    {
        var location = await GetCurrentLocationAsync(clientLatitude, clientLongitude, cancellationToken);
        var weather = await GetLiveWeatherAsync(location.Latitude, location.Longitude, cancellationToken);

        return new LiveGeoLocationAndWeather(location, weather);
    }

    private static (string Condition, string Description) DecodeWmoWeatherCode(int code) => code switch
    {
        0 => ("Ensolarado / Céu Limpo", "Céu completamente limpo e ensolarado"),
        1 or 2 => ("Parcialmente Nublado", "Predomínio de sol com poucas nuvens"),
        3 => ("Nublado", "Céu encoberto por nuvens"),
        45 or 48 => ("Neblina", "Visibilidade reduzida por nevoeiro"),
        51 or 53 or 55 => ("Garoa Leve", "Chuva fraca / chuvisco"),
        61 or 63 or 65 => ("Chuva Moderada", "Chuva persistente na região"),
        80 or 81 or 82 => ("Pancadas de Chuva", "Instabilidade atmosférica com pancadas"),
        95 or 96 or 99 => ("Tempestade Elétrica", "Trovoadas e descargas atmosféricas"),
        _ => ("Tempo Estável", "Condições climáticas regulares")
    };
}
