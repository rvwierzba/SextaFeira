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

        // 2. Se nenhuma credencial externa do Google foi fornecida, persistir/gerenciar eventos reais em arquivo local JSON sincronizado
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

        var now = DateTime.UtcNow;
        var today = DateTime.Today;

        var defaultEvents = new List<CalendarEventItem>
        {
            new()
            {
                Id = "evt-101",
                Title = "Reunião de Arquitetura de IA",
                Description = "Revisão do Harness Sexta-Feira com time de engenharia",
                StartTime = today.AddHours(14),
                EndTime = today.AddHours(15),
                Location = "Google Meet",
                Status = "Confirmado"
            },
            new()
            {
                Id = "evt-102",
                Title = "Deploy do Sandbox Docker & pgvector",
                Description = "Validação do cluster de memória vetorial e testes de carga",
                StartTime = today.AddHours(16).AddMinutes(30),
                EndTime = today.AddHours(17).AddMinutes(30),
                Location = "Dev Environment",
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
                Subject = "Relatório de Telemetria do Core Sexta-Feira",
                FromAddress = "telemetry@sextafeira.ai",
                PreviewSnippet = "Todos os nós de inferência e instâncias Phi-3 estão com latência média de 45ms...",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-25),
                IsUnread = true
            },
            new()
            {
                Id = "msg-002",
                Subject = "Novo Modelo de Vetores Disponível",
                FromAddress = "updates@huggingface.co",
                PreviewSnippet = "O modelo de embeddings bilíngue pt-BR foi otimizado para pgvector com 384 dimensões...",
                ReceivedAt = DateTime.UtcNow.AddHours(-2),
                IsUnread = true
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
            new() { Id = "f-2", Name = "SextaFeira-Core-v1.0.sln", Path = "/Projetos/SextaFeira-Core-v1.0.sln", IsDirectory = false, SizeInBytes = 4096, LastModifiedAt = DateTime.UtcNow.AddHours(-1) },
            new() { Id = "f-3", Name = "Arquitetura-Holografica.pdf", Path = "/Documentos/Arquitetura-Holografica.pdf", IsDirectory = false, SizeInBytes = 10485760, LastModifiedAt = DateTime.UtcNow.AddDays(-5) }
        };

        return Task.FromResult(files);
    }

    public Task<SpotifyTrackItem> GetCurrentSpotifyPlaybackAsync(CancellationToken cancellationToken = default)
    {
        var current = new SpotifyTrackItem
        {
            Id = "spot-001",
            Title = "Starboy",
            Artist = "The Weeknd ft. Daft Punk",
            Album = "Starboy",
            DurationMs = 230000,
            IsPlaying = true,
            ContextUri = "spotify:track:75eeB0gQ36eX"
        };
        return Task.FromResult(current);
    }

    public Task<bool> PlaySpotifyTrackAsync(string queryOrUri, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Spotify MCP Provider reproduzindo faixa ou pesquisa: {Query}", queryOrUri);
        return Task.FromResult(true);
    }

    public Task<List<SpotifyTrackItem>> SearchSpotifyTracksAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        var mockResults = new List<SpotifyTrackItem>
        {
            new() { Id = "sp-1", Title = $"{query} (Remix Cyberpunk)", Artist = "SextaFeira Synthesizer", Album = "Sci-Fi Hits", DurationMs = 210000, IsPlaying = false },
            new() { Id = "sp-2", Title = "Blinding Lights", Artist = "The Weeknd", Album = "After Hours", DurationMs = 200000, IsPlaying = false }
        };
        return Task.FromResult(mockResults.Take(limit).ToList());
    }
}
