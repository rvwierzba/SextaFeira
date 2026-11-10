using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SextaFeira.Application.Agent;

/// <summary>Detecta e remove a palavra de ativação ("Sexta-Feira", "Ei Sexta"...) de uma transcrição.</summary>
public static class WakeWord
{
    private static readonly string[] Fillers = ["ei", "hey", "ok", "oi", "ola", "olá", "e ai", "e aí", "fala", "oye", "hi"];
    private static readonly HashSet<string> FillerTokens = ["ei", "hey", "ok", "okay", "oi", "ola", "e", "ai", "fala", "oye", "hi", "alo", "opa", "eai", "ae"];

    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    /// <summary>
    /// Procura a palavra de ativação nas primeiras palavras da frase.
    /// Retorna true e o comando sem a palavra de ativação (pode ser vazio se a pessoa só chamou).
    /// </summary>
    public static bool TryStrip(string transcript, IEnumerable<string> wakeWords, out string command)
    {
        command = transcript.Trim();
        var normalized = Normalize(transcript);
        if (normalized.Length == 0) return false;

        // Ordena das mais longas para as mais curtas ("sexta feira" antes de "sexta").
        var candidates = wakeWords.Select(Normalize).Where(w => w.Length > 0).Distinct()
            .Select(w => w.Split(' ')).OrderByDescending(w => w.Length).ThenByDescending(w => w.Sum(t => t.Length)).ToList();
        var words = normalized.Split(' ');

        // Antes da palavra de ativação só podem vir até 3 interjeições ("ei", "ok", "oi"...),
        // senão "hoje é sexta?" ativaria a assistente.
        for (var start = 0; start <= Math.Min(3, words.Length - 1); start++)
        {
            if (start > 0 && !FillerTokens.Contains(words[start - 1])) break;
            foreach (var wake in candidates)
            {
                if (start + wake.Length > words.Length) continue;
                var matches = true;
                for (var k = 0; k < wake.Length && matches; k++)
                    matches = SoundsLike(words[start + k], wake[k], allowTypo: wake.Length > 1);
                if (!matches) continue;

                command = DropLeadingWords(transcript, start + wake.Length);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Igualdade tolerante a erros de transcrição ("sesta", "cesta", "sexta"): palavras com 4+ letras
    /// podem diferir em uma letra.
    /// </summary>
    private static bool SoundsLike(string heard, string expected, bool allowTypo)
    {
        if (heard == expected) return true;
        heard = Phonetic(heard);
        expected = Phonetic(expected);
        if (heard == expected) return true;
        if (!allowTypo || expected.Length < 4 || Math.Abs(heard.Length - expected.Length) > 1) return false;
        return EditDistanceAtMostOne(heard, expected);
    }

    /// <summary>Forma fonética simplificada do português: "sexta", "sesta" e "cesta" viram "sesta".</summary>
    private static string Phonetic(string w) =>
        Regex.Replace(w.Replace("ss", "s").Replace("x", "s").Replace("z", "s"), "c(?=[ei])", "s");

    private static bool EditDistanceAtMostOne(string a, string b)
    {
        int i = 0, j = 0, edits = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j]) { i++; j++; continue; }
            if (++edits > 1) return false;
            if (a.Length > b.Length) i++;
            else if (a.Length < b.Length) j++;
            else { i++; j++; }
        }
        return edits + (a.Length - i) + (b.Length - j) <= 1;
    }

    public static bool IsOnlyFiller(string text)
    {
        var n = Normalize(text);
        return n.Length == 0 || Fillers.Contains(n);
    }

    private static string DropLeadingWords(string original, int count)
    {
        var matches = Regex.Matches(original, @"[\p{L}\p{N}]+");
        if (count <= 0 || matches.Count == 0) return original.Trim();
        if (count >= matches.Count) return "";
        var cut = matches[count - 1].Index + matches[count - 1].Length;
        return original[cut..].TrimStart(' ', ',', '.', '!', '?', '-', ':', ';').Trim();
    }
}
