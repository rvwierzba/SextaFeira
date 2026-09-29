using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Infrastructure.Integrations;

public class PersonalIntegrationsService : IPersonalIntegrationsService
{
    private readonly ILogger<PersonalIntegrationsService> _logger;

    public PersonalIntegrationsService(ILogger<PersonalIntegrationsService> logger)
    {
        _logger = logger;
    }

    public Task<List<CalendarEventItem>> GetUpcomingEventsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = DateTime.Today;

        var events = new List<CalendarEventItem>
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
            },
            new()
            {
                Id = "evt-103",
                Title = "Sincronização com Clientes & MCP",
                Description = "Apresentação do ecossistema de agentes autônomos",
                StartTime = today.AddDays(1).AddHours(10),
                EndTime = today.AddDays(1).AddHours(11),
                Location = "Microsoft Teams",
                Status = "Pendente"
            }
        };

        return Task.FromResult(events.Take(count).ToList());
    }

    public Task<CalendarEventItem> CreateCalendarEventAsync(CalendarEventItem eventItem, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventItem.Id))
        {
            eventItem.Id = $"evt-{Guid.NewGuid().ToString().Substring(0, 8)}";
        }
        _logger.LogInformation("Novo evento de calendário agendado: {Title} às {StartTime}", eventItem.Title, eventItem.StartTime);
        return Task.FromResult(eventItem);
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
            },
            new()
            {
                Id = "msg-003",
                Subject = "Alerta de Segurança: Firewall Perimetral",
                FromAddress = "security@cloud.net",
                PreviewSnippet = "Auditoria de portas locais concluída sem anomalias detectadas na sub-rede...",
                ReceivedAt = DateTime.UtcNow.AddHours(-5),
                IsUnread = false
            }
        };

        return Task.FromResult(emails.Take(count).ToList());
    }

    public Task<List<OneDriveFileItem>> GetOneDriveFilesAsync(string folderPath = "/", CancellationToken cancellationToken = default)
    {
        var files = new List<OneDriveFileItem>
        {
            new() { Id = "f-1", Name = "Projetos", Path = "/Projetos", IsDirectory = true, SizeInBytes = 0, LastModifiedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = "f-2", Name = "SextaFeira-Core-v1.0.sln", Path = "/Projetos/SextaFeira-Core-v1.0.sln", IsDirectory = false, SizeInBytes = 4096, LastModifiedAt = DateTime.UtcNow.AddHours(-1) },
            new() { Id = "f-3", Name = "Arquitetura-Holografica.pdf", Path = "/Documentos/Arquitetura-Holografica.pdf", IsDirectory = false, SizeInBytes = 10485760, LastModifiedAt = DateTime.UtcNow.AddDays(-5) },
            new() { Id = "f-4", Name = "Dataset-Treinamento-PTBR.parquet", Path = "/Dados/Dataset-Treinamento-PTBR.parquet", IsDirectory = false, SizeInBytes = 52428800, LastModifiedAt = DateTime.UtcNow.AddDays(-1) },
            new() { Id = "f-5", Name = "Gravacoes-Voz-Whisper.wav", Path = "/Audio/Gravacoes-Voz-Whisper.wav", IsDirectory = false, SizeInBytes = 2097152, LastModifiedAt = DateTime.UtcNow.AddMinutes(-40) }
        };

        return Task.FromResult(files);
    }
}
