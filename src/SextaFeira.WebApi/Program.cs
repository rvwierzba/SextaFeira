using System.Net;
using SextaFeira.Application.Agent;
using SextaFeira.Domain.Agent;
using SextaFeira.Infrastructure.Engines;
using SextaFeira.Infrastructure.Mcp;
using SextaFeira.Infrastructure.Storage;
using SextaFeira.Infrastructure.Tools;
using SextaFeira.Infrastructure.Voice;
using SextaFeira.WebApi.Hosting;
using SextaFeira.WebApi.Realtime;

var builder = WebApplication.CreateBuilder(args);

// Dados locais (configurações, memória, modelos, certificados) ficam em <repo>/data, fora do git.
var repoRoot = DataPaths.FindRepoRoot(builder.Environment.ContentRootPath);
DataPaths.Initialize(builder.Configuration["DataPath"] ?? Path.Combine(repoRoot ?? builder.Environment.ContentRootPath, "data"));

var httpPort = builder.Configuration.GetValue("HttpPort", 5000);
var httpsPort = builder.Configuration.GetValue("HttpsPort", 5001);
// ListenOnLan=false restringe o servidor a este computador (sem microfones de outros dispositivos).
var listenOnLan = builder.Configuration.GetValue("ListenOnLan", true);
var bindAddress = listenOnLan ? IPAddress.Any : IPAddress.Loopback;
var certificate = LanCertificate.LoadOrCreate();
builder.WebHost.ConfigureKestrel(k =>
{
    k.Listen(bindAddress, httpPort);
    k.Listen(bindAddress, httpsPort, o => o.UseHttps(certificate));
});

// ── HTTP ──────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("llm", c => c.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHttpClient("probe", c => c.Timeout = TimeSpan.FromSeconds(3));
builder.Services.AddHttpClient("web", c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0 Safari/537.36");
    c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR,pt;q=0.9");
});

builder.Services.AddHttpClient("open-data", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("SextaFeira/3.0 (assistente pessoal local; .NET)");
});

// ── Núcleo ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ISettingsStore, JsonSettingsStore>();

builder.Services.AddSingleton<OllamaEngine>();
builder.Services.AddSingleton<IChatEngine>(sp => sp.GetRequiredService<OllamaEngine>());
builder.Services.AddSingleton<IChatEngine, OpenAiEngine>();
builder.Services.AddSingleton<IChatEngine, GeminiEngine>();
builder.Services.AddSingleton<IChatEngine, AnthropicEngine>();
builder.Services.AddSingleton<EngineRouter>();

builder.Services.AddSingleton<IMemoryStore, FileMemoryStore>();

builder.Services.AddSingleton(sp => new CodeTools(
    sp.GetRequiredService<ISettingsStore>(), sp.GetRequiredService<IHostApplicationLifetime>(), builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IEnumerable<ITool>>(sp =>
    BuiltinTools.Create(sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<IMemoryStore>(), sp.GetRequiredService<ISettingsStore>())
        .Concat(sp.GetRequiredService<CodeTools>().Create())
        .ToList());

builder.Services.AddSingleton<McpManager>();
builder.Services.AddSingleton<IToolProvider>(sp => sp.GetRequiredService<McpManager>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<McpManager>());

builder.Services.AddSingleton<ToolRegistry>();
builder.Services.AddSingleton<ConversationAgent>();
builder.Services.AddHostedService<EngineWarmupService>();

// ── Voz ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<WhisperSpeechToText>();
builder.Services.AddSingleton<ISpeechToText>(sp => sp.GetRequiredService<WhisperSpeechToText>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WhisperSpeechToText>());
builder.Services.AddSingleton<ITextToSpeech, DeviceTextToSpeech>();

// ── Tempo real ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<AssistantSessionService>();
builder.Services.AddSignalR(o =>
{
    o.MaximumReceiveMessageSize = 16 * 1024 * 1024; // falas longas em PCM
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddControllers();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<AssistantHub>("/hubs/assistant");
app.MapFallbackToFile("index.html");

app.Lifetime.ApplicationStarted.Register(() =>
{
    var log = app.Services.GetRequiredService<ILogger<Program>>();
    log.LogInformation("Sexta-Feira no ar. Neste computador: http://localhost:{Http}", httpPort);
    foreach (var ip in listenOnLan ? LanAddresses() : [])
        log.LogInformation("Outros dispositivos da rede: https://{Ip}:{Https}  (aceite o aviso de certificado)", ip, httpsPort);
});

app.Run();

static IEnumerable<IPAddress> LanAddresses() => LanCertificate.LanAddresses();
