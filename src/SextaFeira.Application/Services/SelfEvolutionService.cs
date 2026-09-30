using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Application.Services;

public class SelfEvolutionService : ISelfEvolutionService
{
    private readonly IEnumerable<ILLMProvider> _llmProviders;
    private readonly ILogger<SelfEvolutionService> _logger;

    public SelfEvolutionService(
        IEnumerable<ILLMProvider> llmProviders,
        ILogger<SelfEvolutionService> logger)
    {
        _llmProviders = llmProviders;
        _logger = logger;
    }

    public async Task<SelfEvolutionResult> EvolveCodebaseAsync(
        string goalInstruction, 
        string? targetFilePath = null, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚀 Iniciando Protocolo de Auto-Evolução de Código. Instrução: {Goal}", goalInstruction);

        var provider = _llmProviders.FirstOrDefault(p => p.ProviderType == LLMProviderType.GoogleGemini || p.ProviderType == LLMProviderType.OpenAI)
                       ?? _llmProviders.First();

        var rootPath = FindWorkspaceRoot();
        var resolvedPath = string.IsNullOrWhiteSpace(targetFilePath) 
            ? Path.Combine(rootPath, "src", "SextaFeira.Application", "Services", "SelfEvolutionService.cs")
            : Path.Combine(rootPath, targetFilePath);

        string originalContent = string.Empty;
        if (File.Exists(resolvedPath))
        {
            originalContent = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
        }

        var prompt = $"""
            Você é o módulo de Auto-Evolução e Self-Coding da SEXTA-FEIRA.
            Seu objetivo é implementar a seguinte funcionalidade ou melhoria no próprio código do sistema:

            INSTRUÇÃO DE MELHORIA:
            {goalInstruction}

            ARQUIVO ALVO:
            {resolvedPath}

            CONTEÚDO ATUAL DO ARQUIVO:
            ```csharp
            {originalContent}
            ```

            DIRETRIZES DE CÓDIGO:
            - Mantenha o código limpo, robusto e compatível com C# / .NET 10.
            - Não quebre namespaces ou referências existentes.
            - Retorne APENAS o código C# completo atualizado/corrigido dentro de bloco ```csharp ... ``` sem introduções.
            """;

        var response = await provider.GenerateAsync(new LLMRequest(prompt), cancellationToken);
        var updatedCode = ExtractCodeFromMarkdown(response.Text);

        if (string.IsNullOrWhiteSpace(updatedCode))
        {
            return new SelfEvolutionResult
            {
                Succeeded = false,
                TargetFile = resolvedPath,
                FeatureDescription = goalInstruction,
                ChangesSummary = "Falha ao gerar o código atualizado pela LLM.",
                BuildLog = "Nenhum código C# válido foi extraído da resposta."
            };
        }

        // Tentar aplicar e verificar compilação do projeto
        var backupPath = resolvedPath + ".bak";
        if (File.Exists(resolvedPath))
        {
            File.Copy(resolvedPath, backupPath, overwrite: true);
        }

        bool buildSuccess = false;
        string buildLog = string.Empty;

        try
        {
            await File.WriteAllTextAsync(resolvedPath, updatedCode, cancellationToken);
            var (success, output) = await RunDotnetBuildAsync(rootPath, cancellationToken);
            buildSuccess = success;
            buildLog = output;

            if (!buildSuccess)
            {
                _logger.LogWarning("Compilação falhou após modificação de auto-evolução. Tentando ciclo de reparo...");
                // Loop de reparo de compilação
                var repairPrompt = $"""
                    A alteração realizada gerou erros de compilação.
                    
                    ERRO DE BUILD:
                    {buildLog}

                    CÓDIGO COM ERRO:
                    ```csharp
                    {updatedCode}
                    ```

                    Corrija o código C# para resolver os erros de compilação. Retorne APENAS o código C# corrigido dentro de bloco ```csharp ... ```.
                    """;

                var repairResponse = await provider.GenerateAsync(new LLMRequest(repairPrompt), cancellationToken);
                var repairedCode = ExtractCodeFromMarkdown(repairResponse.Text);
                if (!string.IsNullOrWhiteSpace(repairedCode))
                {
                    await File.WriteAllTextAsync(resolvedPath, repairedCode, cancellationToken);
                    var (repairedSuccess, repairedOutput) = await RunDotnetBuildAsync(rootPath, cancellationToken);
                    buildSuccess = repairedSuccess;
                    buildLog = repairedOutput;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante protocolo de auto-evolução");
            buildLog = ex.Message;
        }

        if (buildSuccess)
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
            await UpdateChangelogAsync(rootPath, goalInstruction, resolvedPath, cancellationToken);
            _logger.LogInformation("✅ Protocolo de Auto-Evolução concluído com SUCESSO. Código atualizado e validado!");
            return new SelfEvolutionResult
            {
                Succeeded = true,
                TargetFile = resolvedPath,
                FeatureDescription = goalInstruction,
                ChangesSummary = $"Modificação aplicada com sucesso em {Path.GetFileName(resolvedPath)}. Projeto compila sem erros.",
                BuildLog = buildLog
            };
        }
        else
        {
            // Restaurar backup se falhou
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, resolvedPath, overwrite: true);
                File.Delete(backupPath);
            }
            _logger.LogWarning("❌ Protocolo de Auto-Evolução revertido devido a falha na compilação.");
            return new SelfEvolutionResult
            {
                Succeeded = false,
                TargetFile = resolvedPath,
                FeatureDescription = goalInstruction,
                ChangesSummary = "Alteração revertida para o backup original devido a erros de compilação.",
                BuildLog = buildLog
            };
        }
    }

    private static async Task UpdateChangelogAsync(string rootPath, string goalInstruction, string targetFile, CancellationToken cancellationToken)
    {
        var changelogPath = Path.Combine(rootPath, ".agents", "CHANGELOG.md");
        var entry = $"\n- **[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC]** `AUTO-EVOLUTION`: {goalInstruction} (Arquivo: `{Path.GetFileName(targetFile)}`)";
        
        if (File.Exists(changelogPath))
        {
            await File.AppendAllTextAsync(changelogPath, entry, cancellationToken);
        }
    }

    private static async Task<(bool Success, string Output)> RunDotnetBuildAsync(string rootPath, CancellationToken cancellationToken)
    {
        try
        {
            var slnPath = Path.Combine(rootPath, "SextaFeira.sln");
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{slnPath}\" --no-incremental",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = rootPath
            };

            using var proc = Process.Start(psi);
            if (proc == null) return (false, "Não foi possível iniciar o processo dotnet.");

            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            var fullLog = stdout + "\n" + stderr;
            return (proc.ExitCode == 0, fullLog);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string FindWorkspaceRoot()
    {
        var current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "SextaFeira.sln")))
            {
                return current;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        return "/home/rvwierzba/Dev/SextaFeira";
    }

    private static string ExtractCodeFromMarkdown(string text)
    {
        var match = Regex.Match(text, @"```(?:csharp|cs)?\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        return text.Trim();
    }
}
