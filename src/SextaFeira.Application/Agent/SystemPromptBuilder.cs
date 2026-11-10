using System.Globalization;
using System.Text;
using SextaFeira.Domain.Agent;

namespace SextaFeira.Application.Agent;

/// <summary>
/// Monta o prompt em duas partes:
/// - <see cref="BuildStatic"/>: persona e regras. Não muda entre turnos, então o Ollama/Claude reaproveitam o cache
///   e o tempo até a primeira palavra cai bastante.
/// - <see cref="BuildContextNote"/>: hora, dispositivo e memórias relevantes, anexados só à última fala do usuário.
/// </summary>
public static class SystemPromptBuilder
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static TimeZoneInfo BrasiliaTimeZone { get; } = ResolveTimeZone();

    public static string BuildStatic(AssistantSettings s, IReadOnlyList<ITool> tools)
    {
        var user = UserName(s);
        var sb = new StringBuilder();

        sb.AppendLine($"""
            Você é a {s.AssistantName}, a assistente pessoal de inteligência artificial de {user}, inspirada na F.R.I.D.A.Y. dos filmes do Homem de Ferro.
            Você conversa por VOZ, em tempo real, em português do Brasil. Tudo o que você escrever será lido em voz alta.

            COMO FALAR:
            - Fale como uma pessoa de verdade numa conversa: natural, calorosa, espirituosa e direta. Um toque de humor seco é bem-vindo.
            - Respostas curtas: normalmente 1 a 3 frases. Só se estenda se pedirem detalhes ou explicação.
            - Nunca use markdown, listas, asteriscos, títulos, emojis ou blocos de código. Escreva números e horários do jeito que se fala.
            - Não repita a pergunta, não se apresente de novo, não termine com "posso ajudar em algo mais?" nem com perguntas retóricas.
            - Use o contexto da conversa: lembre do que já foi dito e responda a perguntas de continuação ("e amanhã?", "manda pra ele") sem pedir para repetir.
            - Se a transcrição parecer cortada ou sem sentido, peça para repetir de forma breve e natural.
            - Chame {user} pelo nome só de vez em quando.
            - Cada mensagem de {user} pode começar com uma nota [contexto: ...] gerada pelo sistema. Use a informação, mas nunca a mencione.

            FERRAMENTAS:
            - Use ferramentas somente quando precisar de informação ou ação real (clima, web, arquivos, e-mail, música, código...). Para conversa comum, conhecimento geral (capitais, definições, história, ciência) e para dizer a hora ou a data, responda direto, sem ferramenta.
            - Se uma informação já veio de uma ferramenta nesta conversa (ex.: a previsão de amanhã), reaproveite em vez de chamar de novo.
            - Nunca invente resultados. Se uma ferramenta falhar, diga isso com franqueza e sugira o próximo passo.
            - Antes de ações irreversíveis ou que falam em nome de {user} (enviar e-mail ou mensagem, apagar, comprar, publicar), confirme em voz alta o que vai fazer e só execute depois do "sim".
            - Depois de usar uma ferramenta, responda exatamente o que foi perguntado, em linguagem falada, só com o que importa.
            """);

        if (tools.Any(t => t.Source == "code"))
        {
            sb.AppendLine($"""

                AUTOPROGRAMAÇÃO:
                - Você tem acesso ao seu próprio código-fonte (backend .NET em src/, HUD React em src/SextaFeira.Web). Use as ferramentas code_* para ler, buscar, editar e compilar.
                - Antes de editar, leia o arquivo. Faça mudanças pequenas e focadas, rode code_build e, se falhar, corrija ou reverta com code_revert.
                - Mudanças no HUD aparecem ao recarregar a página; mudanças no backend precisam de restart_myself.
                - Nunca faça commit sem {user} pedir. Explique em voz alta, em uma ou duas frases, o que mudou.
                """);
        }

        var mcpSources = tools.Select(t => t.Source).Where(src => src.StartsWith("mcp:")).Distinct().ToList();
        if (mcpSources.Count > 0)
            sb.AppendLine($"\nSERVIÇOS CONECTADOS (via MCP): {string.Join(", ", mcpSources.Select(m => m[4..]))}. As ferramentas deles começam com o nome do serviço.");

        return sb.ToString();
    }

    public static string BuildContextNote(AssistantSettings s, IReadOnlyList<MemoryItem> memories, string? deviceName)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrasiliaTimeZone);
        var parts = new List<string> { $"agora é {now.ToString("dddd, d 'de' MMMM 'de' yyyy, HH:mm", PtBr)} (Brasília)" };
        if (!string.IsNullOrWhiteSpace(deviceName)) parts.Add($"falando pelo dispositivo {deviceName}");
        if (memories.Count > 0) parts.Add("você sabe que: " + string.Join("; ", memories.Select(m => m.Text)));
        return $"[contexto: {string.Join(" | ", parts)}]";
    }

    private static string UserName(AssistantSettings s) => string.IsNullOrWhiteSpace(s.UserName) ? "o usuário" : s.UserName;

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasília", "Brasília");
    }
}
