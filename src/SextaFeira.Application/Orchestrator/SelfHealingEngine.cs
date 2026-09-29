using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Application.Orchestrator;

public record SelfHealingResult(
    bool Succeeded,
    int Iterations,
    string FinalCode,
    string StdOut,
    string? StdErr,
    List<CodeExecutionJob> JobHistory
);

public class SelfHealingEngine
{
    private readonly ISandboxService _sandboxService;
    private readonly IEnumerable<ILLMProvider> _llmProviders;
    private readonly ILogger<SelfHealingEngine> _logger;

    public SelfHealingEngine(
        ISandboxService sandboxService,
        IEnumerable<ILLMProvider> llmProviders,
        ILogger<SelfHealingEngine> logger)
    {
        _sandboxService = sandboxService;
        _llmProviders = llmProviders;
        _logger = logger;
    }

    public async Task<SelfHealingResult> RunLoopAsync(
        string language, 
        string initialGoal, 
        string? existingCode = null, 
        int maxRetries = 3,
        Action<int, string>? onIterationProgress = null,
        CancellationToken cancellationToken = default)
    {
        var jobHistory = new List<CodeExecutionJob>();
        var provider = _llmProviders.FirstOrDefault(p => p.ProviderType != LLMProviderType.OllamaLocal) 
                       ?? _llmProviders.First();

        string currentCode = existingCode ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currentCode))
        {
            onIterationProgress?.Invoke(1, "Gerando código inicial para o objetivo...");
            var initialPrompt = $"Você é uma IA de engenharia de software da Sexta-Feira. Escreva o código completo em {language} para: {initialGoal}. Retorne APENAS o código executável dentro de bloco ```{language} ... ``` sem introduções.";
            var response = await provider.GenerateAsync(new LLMRequest(initialPrompt), cancellationToken);
            currentCode = ExtractCodeFromMarkdown(response.Text, language);
        }

        for (int iteration = 1; iteration <= maxRetries; iteration++)
        {
            onIterationProgress?.Invoke(iteration, $"Executando código no Docker Sandbox isolado (Iteração {iteration}/{maxRetries})...");
            _logger.LogInformation("Executando no Docker Sandbox. Iteração {Iteration}", iteration);

            var job = await _sandboxService.ExecuteCodeAsync(language, currentCode, TimeSpan.FromSeconds(30), cancellationToken);
            job.RetryAttempts = iteration;
            jobHistory.Add(job);

            if (job.Status == ExecutionStatus.Success && job.ExitCode == 0)
            {
                onIterationProgress?.Invoke(iteration, "Código compilado e executado com sucesso no Sandbox!");
                return new SelfHealingResult(
                    Succeeded: true,
                    Iterations: iteration,
                    FinalCode: currentCode,
                    StdOut: job.StdOut ?? string.Empty,
                    StdErr: null,
                    JobHistory: jobHistory
                );
            }

            // Falha na execução - Iniciar ciclo de auto-correção
            onIterationProgress?.Invoke(iteration, $"Falha detectada (ExitCode {job.ExitCode}). Enviando stderr para refatoração do modelo...");
            _logger.LogWarning("Falha na iteração {Iteration}. StdErr: {StdErr}", iteration, job.StdErr);

            var repairPrompt = $"""
                O código a seguir em {language} falhou na execução dentro do container Docker isolado.
                
                OBJETIVO ORIGINAL:
                {initialGoal}
                
                CÓDIGO EXECUTADO:
                ```{language}
                {currentCode}
                ```
                
                ERRO CAPTURADO (STDERR / COMPILAÇÃO):
                {job.StdErr}
                
                STDOUT CAPTURADO:
                {job.StdOut}
                
                Corrija o código para solucionar o erro acima. Retorne APENAS o código corrigido completo dentro de bloco ```{language} ... ```.
                """;

            var repairResponse = await provider.GenerateAsync(new LLMRequest(repairPrompt), cancellationToken);
            currentCode = ExtractCodeFromMarkdown(repairResponse.Text, language);
        }

        var lastJob = jobHistory.LastOrDefault();
        return new SelfHealingResult(
            Succeeded: false,
            Iterations: maxRetries,
            FinalCode: currentCode,
            StdOut: lastJob?.StdOut ?? string.Empty,
            StdErr: lastJob?.StdErr ?? "Excedido limite de tentativas sem sucesso.",
            JobHistory: jobHistory
        );
    }

    private static string ExtractCodeFromMarkdown(string text, string language)
    {
        var tag = $"```{language}";
        int startIndex = text.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
        if (startIndex != -1)
        {
            startIndex += tag.Length;
            if (startIndex < text.Length && text[startIndex] == '\n') startIndex++;
            int endIndex = text.IndexOf("```", startIndex, StringComparison.Ordinal);
            if (endIndex != -1)
            {
                return text.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }
        return text.Trim();
    }
}
