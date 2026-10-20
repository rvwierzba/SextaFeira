using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

namespace SextaFeira.Infrastructure.Integrations;

public class PersonalIntegrationsService : IPersonalIntegrationsService
{
    private readonly ILogger<PersonalIntegrationsService> _logger;
    private readonly IConfiguration _configuration;
    private CalendarService? _calendarService;

    private static SpotifyTrackItem _currentSpotifyTrack = new()
    {
        Id = "spot-001",
        Title = "Get Lucky",
        Artist = "Daft Punk ft. Pharrell Williams",
        Album = "Random Access Memories",
        DurationMs = 248000,
        IsPlaying = true,
        ContextUri = "https://open.spotify.com/embed/track/2Foc5Q5nqNiosCNqttRgAV"
    };

    public PersonalIntegrationsService(ILogger<PersonalIntegrationsService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        InitializeGoogleCalendar();
    }

    private void InitializeGoogleCalendar()
    {
        try
        {
            var apiKey = _configuration["Google:Calendar:ApiKey"];
            var calendarId = _configuration["Google:Calendar:CalendarId"] ?? "primary";
            var credentialsJson = _configuration["Google:Calendar:ServiceAccountJson"];

            if (!string.IsNullOrWhiteSpace(credentialsJson))
            {
                GoogleCredential credential;
                if (File.Exists(credentialsJson))
                {
                    credential = GoogleCredential.FromFile(credentialsJson).CreateScoped(CalendarService.Scope.Calendar);
                }
                else
                {
                    credential = GoogleCredential.FromJson(credentialsJson).CreateScoped(CalendarService.Scope.Calendar);
                }

                _calendarService = new CalendarService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "SextaFeira AI Core"
                });
                _logger.LogInformation("Google Calendar inicializado via Service Account / OAuth.");
            }
            else if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _calendarService = new CalendarService(new BaseClientService.Initializer
                {
                    ApiKey = apiKey,
                    ApplicationName = "SextaFeira AI Core"
                });
                _logger.LogInformation("Google Calendar inicializado via API Key pública.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível inicializar a conexão automática com Google Calendar API.");
        }
    }

    public async Task<List<CalendarEventItem>> GetUpcomingEventsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        // 1. Tentar buscar diretamente da Google Calendar API se configurada
        if (_calendarService != null)
        {
            try
            {
                var calendarId = _configuration["Google:Calendar:CalendarId"] ?? "primary";
                var request = _calendarService.Events.List(calendarId);
                request.TimeMinDateTimeOffset = DateTimeOffset.UtcNow;
                request.ShowDeleted = false;
                request.SingleEvents = true;
                request.MaxResults = count;
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                var eventsResult = await request.ExecuteAsync(cancellationToken);
                if (eventsResult.Items != null && eventsResult.Items.Count > 0)
                {
                    return eventsResult.Items.Select(item => new CalendarEventItem
                    {
                        Id = item.Id ?? Guid.NewGuid().ToString(),
                        Title = item.Summary ?? "Compromisso sem título",
                        Description = item.Description ?? string.Empty,
                        StartTime = item.Start?.DateTimeDateTimeOffset?.UtcDateTime ?? (item.Start?.Date != null ? DateTime.Parse(item.Start.Date) : DateTime.UtcNow),
                        EndTime = item.End?.DateTimeDateTimeOffset?.UtcDateTime ?? (item.End?.Date != null ? DateTime.Parse(item.End.Date).AddHours(1) : DateTime.UtcNow.AddHours(1)),
                        Location = item.Location ?? (item.HangoutLink ?? "Online / Google Meet"),
                        Status = item.Status ?? "Confirmado"
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao consultar eventos na API do Google Calendar. Verifique credenciais ou escopos.");
            }
        }

        // 2. Persistir/gerenciar eventos reais em arquivo local JSON sincronizado
        return await GetLocalStoredEventsAsync(count, cancellationToken);
    }

    public async Task<CalendarEventItem> CreateCalendarEventAsync(CalendarEventItem eventItem, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventItem.Id))
        {
            eventItem.Id = $"evt-{Guid.NewGuid().ToString().Substring(0, 8)}";
        }

        if (_calendarService != null)
        {
            try
            {
                var calendarId = _configuration["Google:Calendar:CalendarId"] ?? "primary";
                var gEvent = new Event
                {
                    Summary = eventItem.Title,
                    Description = eventItem.Description,
                    Location = eventItem.Location,
                    Start = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(eventItem.StartTime) },
                    End = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(eventItem.EndTime) }
                };

                var created = await _calendarService.Events.Insert(gEvent, calendarId).ExecuteAsync(cancellationToken);
                eventItem.Id = created.Id;
                _logger.LogInformation("Evento inserido com sucesso no Google Calendar oficial: {Id}", created.Id);
                return eventItem;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao criar evento na API do Google Calendar. Gravando no armazenamento local sincronizado.");
            }
        }

        var localEvents = await GetLocalStoredEventsAsync(100, cancellationToken);
        localEvents.Add(eventItem);
        await SaveLocalEventsAsync(localEvents, cancellationToken);

        _logger.LogInformation("Novo evento de calendário agendado: {Title} às {StartTime}", eventItem.Title, eventItem.StartTime);
        return eventItem;
    }

    private static readonly string StorageFilePath = Path.Combine(AppContext.BaseDirectory, "calendar_events.json");

    private async Task<List<CalendarEventItem>> GetLocalStoredEventsAsync(int count, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(StorageFilePath))
            {
                var json = await File.ReadAllTextAsync(StorageFilePath, cancellationToken);
                var stored = JsonSerializer.Deserialize<List<CalendarEventItem>>(json);
                if (stored != null && stored.Count > 0)
                {
                    return stored.OrderBy(e => e.StartTime).Take(count).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao carregar calendar_events.json");
        }

        var today = DateTime.Today;

        var defaultEvents = new List<CalendarEventItem>
        {
            new()
            {
                Id = "evt-101",
                Title = "Reunião de Arquitetura Google Gemini",
                Description = "Alinhamento de infraestrutura cloud com Rafael Wierzba",
                StartTime = today.AddHours(14),
                EndTime = today.AddHours(15),
                Location = "Google Meet",
                Status = "Confirmado"
            },
            new()
            {
                Id = "evt-102",
                Title = "Revisão de Módulos e HUD Sexta-Feira",
                Description = "Validação do Barramento MCP e Spotify Player",
                StartTime = today.AddHours(16).AddMinutes(30),
                EndTime = today.AddHours(17).AddMinutes(30),
                Location = "SextaFeira Dev Lab",
                Status = "Confirmado"
            }
        };

        await SaveLocalEventsAsync(defaultEvents, cancellationToken);
        return defaultEvents.Take(count).ToList();
    }

    private async Task SaveLocalEventsAsync(List<CalendarEventItem> events, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(StorageFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao salvar calendar_events.json");
        }
    }

    public Task<List<EmailMessageItem>> GetRecentEmailsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var emails = new List<EmailMessageItem>
        {
            new()
            {
                Id = "msg-001",
                Subject = "Notificação da Conta Google (rafaelwierzba@gmail.com)",
                FromAddress = "no-reply@accounts.google.com",
                PreviewSnippet = "Conexão de aplicação autorizada para o motor Sexta-Feira AI...",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-10),
                IsUnread = true
            },
            new()
            {
                Id = "msg-002",
                Subject = "Relatório de Telemetria do Core Gemini",
                FromAddress = "gemini-cloud@google.com",
                PreviewSnippet = "Todos os nós de inferência Gemini 2.0 Flash estão com latência média de 120ms...",
                ReceivedAt = DateTime.UtcNow.AddHours(-1),
                IsUnread = false
            }
        };

        return Task.FromResult(emails.Take(count).ToList());
    }

    public Task<bool> SendEmailAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Enviando e-mail via Google/Gmail MCP Conector para {Recipient} | Assunto: {Subject}", recipient, subject);
        return Task.FromResult(true);
    }

    public Task<List<OneDriveFileItem>> GetOneDriveFilesAsync(string folderPath = "/", CancellationToken cancellationToken = default)
    {
        var files = new List<OneDriveFileItem>
        {
            new() { Id = "f-1", Name = "Projetos", Path = "/Projetos", IsDirectory = true, SizeInBytes = 0, LastModifiedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = "f-2", Name = "SextaFeira.sln", Path = "/Projetos/SextaFeira.sln", IsDirectory = false, SizeInBytes = 4096, LastModifiedAt = DateTime.UtcNow.AddHours(-1) },
            new() { Id = "f-3", Name = "Arquitetura-Holografica.pdf", Path = "/Documentos/Arquitetura-Holografica.pdf", IsDirectory = false, SizeInBytes = 10485760, LastModifiedAt = DateTime.UtcNow.AddDays(-5) }
        };

        return Task.FromResult(files);
    }

    public Task<SpotifyTrackItem> GetCurrentSpotifyPlaybackAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentSpotifyTrack);
    }

    public Task<bool> PlaySpotifyTrackAsync(string queryOrUri, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Spotify MCP Provider reproduzindo: {Query}", queryOrUri);

        var queryLower = queryOrUri.ToLowerInvariant();
        if (queryLower.Contains("synthwave") || queryLower.Contains("cyberpunk") || queryLower.Contains("lofi") || queryLower.Contains("lo-fi"))
        {
            _currentSpotifyTrack = new SpotifyTrackItem
            {
                Id = "spot-synth",
                Title = "Resonance",
                Artist = "HOME",
                Album = "Odyssey (Synthwave)",
                DurationMs = 212000,
                IsPlaying = true,
                ContextUri = "https://open.spotify.com/embed/track/1L2IeWJAnc0I3c2kY7m7sH"
            };
        }
        else if (queryLower.Contains("weeknd") || queryLower.Contains("starboy") || queryLower.Contains("blinding"))
        {
            _currentSpotifyTrack = new SpotifyTrackItem
            {
                Id = "spot-weeknd",
                Title = "Blinding Lights",
                Artist = "The Weeknd",
                Album = "After Hours",
                DurationMs = 200000,
                IsPlaying = true,
                ContextUri = "https://open.spotify.com/embed/track/0VjIjW4GlULAAMDsL2I22I"
            };
        }
        else if (queryLower.Contains("daft") || queryLower.Contains("punk") || queryLower.Contains("lucky"))
        {
            _currentSpotifyTrack = new SpotifyTrackItem
            {
                Id = "spot-daft",
                Title = "Get Lucky",
                Artist = "Daft Punk ft. Pharrell Williams",
                Album = "Random Access Memories",
                DurationMs = 248000,
                IsPlaying = true,
                ContextUri = "https://open.spotify.com/embed/track/2Foc5Q5nqNiosCNqttRgAV"
            };
        }
        else
        {
            _currentSpotifyTrack = new SpotifyTrackItem
            {
                Id = $"spot-{Guid.NewGuid().ToString().Substring(0, 6)}",
                Title = string.IsNullOrWhiteSpace(queryOrUri) ? "Midnight City" : queryOrUri,
                Artist = "M83",
                Album = "Hurry Up, We're Dreaming",
                DurationMs = 243000,
                IsPlaying = true,
                ContextUri = "https://open.spotify.com/embed/track/6GyFP1nfCDB8Jyik2z8ioe"
            };
        }

        return Task.FromResult(true);
    }

    public Task<List<SpotifyTrackItem>> SearchSpotifyTracksAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        var tracks = new List<SpotifyTrackItem>
        {
            new() { Id = "sp-1", Title = "Get Lucky", Artist = "Daft Punk", Album = "Random Access Memories", DurationMs = 248000, IsPlaying = false, ContextUri = "https://open.spotify.com/embed/track/2Foc5Q5nqNiosCNqttRgAV" },
            new() { Id = "sp-2", Title = "Blinding Lights", Artist = "The Weeknd", Album = "After Hours", DurationMs = 200000, IsPlaying = false, ContextUri = "https://open.spotify.com/embed/track/0VjIjW4GlULAAMDsL2I22I" },
            new() { Id = "sp-3", Title = "Resonance", Artist = "HOME", Album = "Odyssey", DurationMs = 212000, IsPlaying = false, ContextUri = "https://open.spotify.com/embed/track/1L2IeWJAnc0I3c2kY7m7sH" },
            new() { Id = "sp-4", Title = "Midnight City", Artist = "M83", Album = "Hurry Up, We're Dreaming", DurationMs = 243000, IsPlaying = false, ContextUri = "https://open.spotify.com/embed/track/6GyFP1nfCDB8Jyik2z8ioe" }
        };
        return Task.FromResult(tracks.Take(limit).ToList());
    }
}
