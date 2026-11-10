using System.Text;
using System.Text.RegularExpressions;

namespace SextaFeira.Application.Agent;

/// <summary>
/// Recebe o texto do LLM em pedaços (streaming) e devolve frases completas assim que ficam prontas,
/// para que o TTS comece a falar a primeira frase enquanto o resto ainda está sendo gerado.
/// </summary>
public sealed class SentenceSplitter
{
    private static readonly string[] Abbreviations =
        ["sr.", "sra.", "dr.", "dra.", "prof.", "etc.", "ex.", "obs.", "av.", "n.º", "nº.", "vs.", "aprox."];

    private readonly StringBuilder _buffer = new();
    private readonly int _minLength;
    private readonly int _softMaxLength;

    public SentenceSplitter(int minLength = 8, int softMaxLength = 220)
    {
        _minLength = minLength;
        _softMaxLength = softMaxLength;
    }

    public IEnumerable<string> Push(string delta)
    {
        _buffer.Append(delta);
        while (TryExtract(out var sentence))
            yield return sentence;
    }

    public string? Flush()
    {
        var rest = _buffer.ToString().Trim();
        _buffer.Clear();
        return rest.Length > 0 ? rest : null;
    }

    private bool TryExtract(out string sentence)
    {
        sentence = "";
        var text = _buffer.ToString();

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var isTerminator = c is '.' or '!' or '?' or '…' or ';' or ':';
            var isNewline = c == '\n';
            if (!isTerminator && !isNewline) continue;

            // Precisa de um caractere depois para saber que a frase acabou (evita cortar "3.5" ou "...").
            if (i + 1 >= text.Length) return false;
            if (isTerminator && !char.IsWhiteSpace(text[i + 1]) && text[i + 1] != '"' && text[i + 1] != '\'') continue;

            var candidate = text[..(i + 1)].Trim();
            if (candidate.Length < _minLength && !isNewline) continue;
            if (c == '.' && EndsWithAbbreviation(candidate)) continue;
            if (c == ':' && candidate.Length < 30) continue; // "Olha só:" fica junto da próxima frase

            if (candidate.Length == 0) { _buffer.Remove(0, i + 1); return TryExtract(out sentence); }

            sentence = candidate;
            _buffer.Remove(0, i + 1);
            return true;
        }

        // Frase muito longa sem pontuação: corta na última vírgula para não atrasar a fala.
        if (text.Length > _softMaxLength)
        {
            var comma = text.LastIndexOf(", ", StringComparison.Ordinal);
            if (comma > _minLength)
            {
                sentence = text[..(comma + 1)].Trim();
                _buffer.Remove(0, comma + 1);
                return true;
            }
        }

        return false;
    }

    private static bool EndsWithAbbreviation(string candidate)
    {
        var lower = candidate.ToLowerInvariant();
        foreach (var abbr in Abbreviations)
        {
            if (lower.EndsWith(" " + abbr) || lower == abbr) return true;
        }
        // Iniciais: "J. R. R."
        return Regex.IsMatch(candidate, @"(^|\s)\p{Lu}\.$");
    }
}

/// <summary>Limpa marcações de texto escrito que soam mal quando lidas em voz alta.</summary>
public static class SpeechText
{
    private static readonly Regex CodeBlock = new(@"```.*?```", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Markdown = new(@"[*_#`>|~]+", RegexOptions.Compiled);
    private static readonly Regex Bullet = new(@"^\s*(?:[-•]|\d+[.)])\s+", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex Link = new(@"\[([^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex Url = new(@"https?://\S+", RegexOptions.Compiled);
    private static readonly Regex Emoji = new(@"[☀-➿]|[\uD83C-\uDBFF][\uDC00-\uDFFF]", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    public static string Clean(string text)
    {
        var t = CodeBlock.Replace(text, " ");
        t = Link.Replace(t, "$1");
        t = Url.Replace(t, "o link");
        t = Bullet.Replace(t, "");
        t = Markdown.Replace(t, "");
        t = Emoji.Replace(t, "");
        return Spaces.Replace(t, " ").Trim();
    }
}
