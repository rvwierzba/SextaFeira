using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using SextaFeira.Domain.Agent;
using SextaFeira.Infrastructure.Storage;

namespace SextaFeira.Infrastructure.Tools;

/// <summary>
/// Ferramentas de autoprogramação: a assistente lê, busca, edita e compila o próprio código.
/// Todas as operações ficam presas à raiz do repositório e não fazem commit sozinhas.
/// </summary>
public sealed class CodeTools
{
    public const int RestartExitCode = 42;

    private readonly ISettingsStore _settings;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly string _fallbackRoot;

    public CodeTools(ISettingsStore settings, IHostApplicationLifetime lifetime, string contentRoot)
    {
        _settings = settings;
        _lifetime = lifetime;
        _fallbackRoot = DataPaths.FindRepoRoot(contentRoot) ?? DataPaths.FindRepoRoot(AppContext.BaseDirectory) ?? contentRoot;
    }

    public string RepoRoot =>
        string.IsNullOrWhiteSpace(_settings.Current.SelfCode.RepoPath) ? _fallbackRoot : Path.GetFullPath(_settings.Current.SelfCode.RepoPath);

    public IEnumerable<ITool> Create()
    {
        yield return new FunctionTool("code", "code_list_files",
            "Lista arquivos versionados do seu próprio código-fonte. Filtro opcional por trecho do caminho (ex.: 'Voice', '.tsx').",
            """{"type":"object","properties":{"filter":{"type":"string"}}}""",
            async (a, _, ct) =>
            {
                var (_, output) = await RunAsync("git", "ls-files", ct);
                var filter = a.Str("filter");
                var files = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(f => string.IsNullOrEmpty(filter) || f.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .Take(300);
                return string.Join("\n", files);
            });

        yield return new FunctionTool("code", "code_read_file",
            "Lê um arquivo do seu código-fonte, com números de linha. Use start_line/end_line para arquivos grandes.",
            """{"type":"object","properties":{"path":{"type":"string"},"start_line":{"type":"integer"},"end_line":{"type":"integer"}},"required":["path"]}""",
            async (a, _, ct) =>
            {
                var full = Resolve(a.Required("path"));
                if (!File.Exists(full)) return "Erro: arquivo não existe.";
                var lines = await File.ReadAllLinesAsync(full, ct);
                var start = Math.Max(1, a.Int("start_line") ?? 1);
                var end = Math.Min(lines.Length, a.Int("end_line") ?? Math.Min(lines.Length, start + 399));
                var sb = new StringBuilder();
                for (var i = start; i <= end; i++) sb.Append(i).Append('\t').AppendLine(lines[i - 1]);
                if (end < lines.Length) sb.AppendLine($"… ({lines.Length - end} linhas restantes; use start_line={end + 1})");
                return sb.ToString();
            });

        yield return new FunctionTool("code", "code_search",
            "Busca uma expressão regular no seu código-fonte e retorna arquivo:linha: trecho.",
            """{"type":"object","properties":{"pattern":{"type":"string"},"filter":{"type":"string","description":"trecho do caminho para restringir"}},"required":["pattern"]}""",
            async (a, _, ct) =>
            {
                var regex = new Regex(a.Required("pattern"), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
                var filter = a.Str("filter");
                var (_, list) = await RunAsync("git", "ls-files", ct);
                var hits = new List<string>();
                foreach (var rel in list.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!string.IsNullOrEmpty(filter) && !rel.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                    if (rel.EndsWith(".json") && rel.Contains("lock")) continue;
                    var full = Path.Combine(RepoRoot, rel);
                    if (!File.Exists(full) || new FileInfo(full).Length > 400_000) continue;
                    var n = 0;
                    foreach (var line in await File.ReadAllLinesAsync(full, ct))
                    {
                        n++;
                        if (regex.IsMatch(line)) hits.Add($"{rel}:{n}: {line.Trim()[..Math.Min(line.Trim().Length, 160)]}");
                        if (hits.Count >= 60) break;
                    }
                    if (hits.Count >= 60) break;
                }
                return hits.Count == 0 ? "Nenhuma ocorrência." : string.Join("\n", hits);
            });

        yield return new FunctionTool("code", "code_replace",
            "Edita um arquivo trocando um trecho exato (old_text, que deve aparecer uma única vez) por new_text. Prefira esta à reescrita completa.",
            """{"type":"object","properties":{"path":{"type":"string"},"old_text":{"type":"string"},"new_text":{"type":"string"}},"required":["path","old_text","new_text"]}""",
            async (a, _, ct) =>
            {
                var full = Resolve(a.Required("path"));
                if (!File.Exists(full)) return "Erro: arquivo não existe.";
                var content = await File.ReadAllTextAsync(full, ct);
                var oldText = a.Required("old_text");
                var count = CountOccurrences(content, oldText);
                if (count == 0) return "Erro: old_text não encontrado. Leia o arquivo de novo e copie o trecho exato.";
                if (count > 1) return $"Erro: old_text aparece {count} vezes. Inclua mais contexto para ficar único.";
                await File.WriteAllTextAsync(full, content.Replace(oldText, a.Str("new_text") ?? ""), ct);
                return "Arquivo editado. Rode code_build para validar.";
            });

        yield return new FunctionTool("code", "code_write_file",
            "Cria um arquivo novo (ou reescreve um inteiro) no seu código-fonte.",
            """{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"}},"required":["path","content"]}""",
            async (a, _, ct) =>
            {
                var full = Resolve(a.Required("path"));
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                var existed = File.Exists(full);
                await File.WriteAllTextAsync(full, a.Str("content") ?? "", ct);
                return existed ? "Arquivo reescrito." : "Arquivo criado.";
            });

        yield return new FunctionTool("code", "code_build",
            "Compila o backend (.NET) e o HUD (React) para validar as mudanças. Retorna os erros, se houver.",
            FunctionTool.NoParams,
            async (_, _, ct) =>
            {
                // --artifacts-path evita conflito com os binários em uso pelo processo atual.
                var artifacts = Path.Combine(DataPaths.Root, "selfcode-build");
                var (code, output) = await RunAsync("dotnet", $"build SextaFeira.sln -nologo -v q --artifacts-path \"{artifacts}\"", ct, TimeSpan.FromMinutes(5));
                var report = new StringBuilder();
                var errors = output.Split('\n').Where(l => l.Contains(" error ") || l.Contains("error CS")).Distinct().Take(25).ToList();
                report.AppendLine(code == 0 ? "Backend: compilou OK." : $"Backend: FALHOU.\n{string.Join("\n", errors)}");

                var web = Path.Combine(RepoRoot, "src", "SextaFeira.Web");
                if (Directory.Exists(Path.Combine(web, "node_modules")))
                {
                    var (wcode, wout) = await RunAsync(OperatingSystem.IsWindows() ? "npm.cmd" : "npm", "run build", ct, TimeSpan.FromMinutes(5), web);
                    var werr = wout.Split('\n').Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(20);
                    report.AppendLine(wcode == 0 ? "HUD: build OK (recarregue a página para ver)." : $"HUD: FALHOU.\n{string.Join("\n", werr)}");
                }
                return report.ToString();
            });

        yield return new FunctionTool("code", "code_diff",
            "Mostra o que mudou no código desde o último commit (git status + diff resumido).",
            FunctionTool.NoParams,
            async (_, _, ct) =>
            {
                var (_, status) = await RunAsync("git", "status --short", ct);
                var (_, diff) = await RunAsync("git", "diff --stat", ct);
                return string.IsNullOrWhiteSpace(status) ? "Nenhuma mudança pendente." : $"{status}\n{diff}";
            });

        yield return new FunctionTool("code", "code_revert",
            "Desfaz mudanças não commitadas em um arquivo (ou em todos, se path for vazio). Use quando um build falhar e você não souber corrigir.",
            """{"type":"object","properties":{"path":{"type":"string"}}}""",
            async (a, _, ct) =>
            {
                var path = a.Str("path");
                if (string.IsNullOrWhiteSpace(path))
                {
                    await RunAsync("git", "checkout -- .", ct);
                    return "Todas as mudanças em arquivos versionados foram desfeitas.";
                }
                var full = Resolve(path);
                var rel = Path.GetRelativePath(RepoRoot, full).Replace('\\', '/');
                var (tracked, _) = await RunAsync("git", $"ls-files --error-unmatch \"{rel}\"", ct);
                if (tracked == 0) { await RunAsync("git", $"checkout -- \"{rel}\"", ct); return "Arquivo restaurado."; }
                if (File.Exists(full)) { File.Delete(full); return "Arquivo novo removido."; }
                return "Nada para desfazer.";
            });

        yield return new FunctionTool("code", "code_commit",
            "Faz commit de todas as mudanças. SÓ use quando o usuário pedir explicitamente para salvar/commitar.",
            """{"type":"object","properties":{"message":{"type":"string"}},"required":["message"]}""",
            async (a, _, ct) =>
            {
                await RunAsync("git", "add -A", ct);
                var msg = a.Required("message").Replace("\"", "'");
                var (code, output) = await RunAsync("git", $"commit -m \"{msg}\"", ct);
                return code == 0 ? "Commit feito." : $"Commit falhou: {output}";
            });

        yield return new FunctionTool("code", "restart_myself",
            "Reinicia o seu próprio servidor para aplicar mudanças no backend (só funciona quando iniciado pelo start.ps1). Rode code_build antes.",
            FunctionTool.NoParams,
            (_, _, _) =>
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(4000); // dá tempo de falar a resposta
                    Environment.ExitCode = RestartExitCode;
                    _lifetime.StopApplication();
                });
                return Task.FromResult("Reiniciando em alguns segundos. Volto já.");
            });
    }

    private string Resolve(string relative)
    {
        var root = Path.GetFullPath(RepoRoot);
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && full != root)
            throw new UnauthorizedAccessException("Caminho fora do repositório.");
        var rel = Path.GetRelativePath(root, full);
        if (rel.StartsWith(".git") || rel.Split(Path.DirectorySeparatorChar).Contains("data"))
            throw new UnauthorizedAccessException("Esse caminho é protegido.");
        return full;
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(value, idx, StringComparison.Ordinal)) >= 0) { count++; idx += value.Length; }
        return count;
    }

    private async Task<(int Code, string Output)> RunAsync(string file, string args, CancellationToken ct, TimeSpan? timeout = null, string? cwd = null)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = cwd ?? RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Não consegui iniciar {file}.");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
        var stdout = proc.StandardOutput.ReadToEndAsync(cts.Token);
        var stderr = proc.StandardError.ReadToEndAsync(cts.Token);
        try { await proc.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { try { proc.Kill(true); } catch { } throw; }
        return (proc.ExitCode, (await stdout) + (await stderr));
    }
}
