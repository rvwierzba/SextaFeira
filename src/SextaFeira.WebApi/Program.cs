using Microsoft.EntityFrameworkCore;
using SextaFeira.Application.Orchestrator;
using SextaFeira.Application.Services;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;
using SextaFeira.Infrastructure.Geolocation;
using SextaFeira.Infrastructure.Integrations;
using SextaFeira.Infrastructure.LLMs;
using SextaFeira.Infrastructure.MCP;
using SextaFeira.Infrastructure.Network;
using SextaFeira.Infrastructure.Persistence;
using SextaFeira.Infrastructure.Sandbox;
using SextaFeira.Infrastructure.Stealth;
using SextaFeira.Infrastructure.Voice;
using SextaFeira.WebApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

// Configuração do CORS para o HUD React Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// DbContext PostgreSQL + pgvector
var connectionString = builder.Configuration.GetConnectionString("PostgresVector") 
                       ?? "Host=localhost;Port=5432;Database=sextafeira_db;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, o => o.UseVector());
});

// Gerenciador de Contas e Conexões de IA (Google, OpenAI, Anthropic)
builder.Services.AddSingleton<IAiConnectionManager, AiConnectionManager>();

// Provedores Oficiais de Nuvem de Alta Velocidade (Gemini 2.0 Flash, GPT-4o, Claude 3.5 Sonnet)
builder.Services.AddSingleton<ILLMProvider, GoogleGeminiCloudProvider>();
builder.Services.AddSingleton<ILLMProvider, OpenAiGptCloudProvider>();
builder.Services.AddSingleton<ILLMProvider, AnthropicClaudeCloudProvider>();
builder.Services.AddSingleton<ILLMProvider, OllamaLocalFallbackProvider>();

// Serviços de Infraestrutura e Domínio
builder.Services.AddScoped<IMemoryStore, PgVectorMemoryStore>();
builder.Services.AddSingleton<IGeolocationService, LiveGeolocationService>();
builder.Services.AddSingleton<INetworkScanner, LocalNetworkScanner>();
builder.Services.AddSingleton<IStealthBrowser, PlaywrightStealthBrowser>();
builder.Services.AddSingleton<ISandboxService, DockerContainerRunner>();
builder.Services.AddSingleton<IPersonalIntegrationsService, PersonalIntegrationsService>();
builder.Services.AddSingleton<IVoiceEngine, VoiceEngineService>();
builder.Services.AddScoped<IMcpHost, McpServerRegistry>();

// Orquestradores e Motores da Aplicação
builder.Services.AddScoped<CognitiveOrchestrator>();
builder.Services.AddScoped<SelfHealingEngine>();
builder.Services.AddScoped<MemoryRecyclerService>();
builder.Services.AddScoped<ISelfEvolutionService, SelfEvolutionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();
app.MapHub<HudTelemetryHub>("/hubs/telemetry");
app.MapHub<VoiceStreamHub>("/hubs/voice");

app.Run();
