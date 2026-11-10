using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SextaFeira.Domain.Agent;

namespace SextaFeira.Infrastructure.Tools;

/// <summary>Ferramentas gerais que funcionam sem nenhuma chave de API.</summary>
public static class BuiltinTools
{
    public static IEnumerable<ITool> Create(IHttpClientFactory http, IMemoryStore memory, ISettingsStore settings)
    {
        yield return new FunctionTool("builtin", "get_weather",
            "Clima atual e previsão dos próximos dias. Sem cidade, usa a localização aproximada do computador.",
            """{"type":"object","properties":{"city":{"type":"string","description":"Cidade, ex.: São Paulo"}}}""",
            (a, _, ct) => WeatherAsync(http, a.Str("city"), ct));

        yield return new FunctionTool("builtin", "web_search",
            "Pesquisa notícias e fatos atuais (Wikipédia + Google Notícias). Use só para coisas recentes ou que você não sabe; conhecimento geral responda direto.",
            """{"type":"object","properties":{"query":{"type":"string","description":"Termos de busca curtos"}},"required":["query"]}""",
            (a, _, ct) => WebSearchAsync(http, settings.Current.SearxngUrl, a.Required("query"), ct));

        yield return new FunctionTool("builtin", "read_webpage",
            "Abre uma página da web e retorna o texto principal (para ler um resultado de busca ou link).",
            """{"type":"object","properties":{"url":{"type":"string"}},"required":["url"]}""",
            (a, _, ct) => ReadWebpageAsync(http, a.Required("url"), ct));

        yield return new FunctionTool("builtin", "remember",
            "Guarda um fato duradouro sobre o usuário ou suas preferências na memória de longo prazo (ex.: 'prefere café sem açúcar').",
            """{"type":"object","properties":{"fact":{"type":"string"}},"required":["fact"]}""",
            async (a, _, ct) => { await memory.SaveAsync(a.Required("fact"), ct); return "Memorizado."; });

        // Não há ferramenta "recall": as memórias relevantes já entram sozinhas no contexto de cada fala.
        yield return new FunctionTool("builtin", "forget",
            "Esquece algo que o usuário pediu para você parar de lembrar.",
            """{"type":"object","properties":{"about":{"type":"string","description":"o assunto a esquecer"}},"required":["about"]}""",
            async (a, _, ct) =>
            {
                var matches = await memory.SearchAsync(a.Required("about"), 3, ct);
                if (matches.Count == 0) return "Não havia nada sobre isso na memória.";
                foreach (var m in matches) await memory.DeleteAsync(m.Id, ct);
                return $"Esquecido: {string.Join("; ", matches.Select(m => m.Text))}.";
            });

        yield return new FunctionTool("builtin", "open_on_computer",
            "Abre um site no navegador do computador principal (ex.: YouTube, Spotify Web, um link).",
            """{"type":"object","properties":{"url":{"type":"string","description":"URL completa começando com https://"}},"required":["url"]}""",
            (a, _, _) =>
            {
                var url = a.Required("url");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                    return Task.FromResult("Erro: só abro links http(s).");
                try
                {
                    // Windows/macOS/Linux com desktop: abre no navegador padrão (shell/open/xdg-open).
                    Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
                    return Task.FromResult($"Abri {uri.Host} no computador.");
                }
                catch (Exception)
                {
                    return Task.FromResult("Erro: este servidor não tem navegador (roda sem tela ou em container). Diga o link ao usuário.");
                }
            });
    }

    // ── Clima (Open-Meteo, sem chave) ─────────────────────────────────────────

    private static async Task<string> WeatherAsync(IHttpClientFactory http, string? city, CancellationToken ct)
    {
        var client = http.CreateClient("web");
        double lat, lon;
        string place;

        if (!string.IsNullOrWhiteSpace(city))
        {
            var geo = await client.GetFromJsonAsync<JsonElement>(
                $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=pt&format=json", ct);
            if (!geo.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return $"Não encontrei a cidade '{city}'.";
            var r = results[0];
            lat = r.GetProperty("latitude").GetDouble();
            lon = r.GetProperty("longitude").GetDouble();
            place = $"{r.GetProperty("name").GetString()}, {(r.TryGetProperty("admin1", out var adm) ? adm.GetString() : "")}";
        }
        else
        {
            var ip = await client.GetFromJsonAsync<JsonElement>("https://ipwho.is/?fields=city,region,latitude,longitude,success", ct);
            if (!ip.TryGetProperty("success", out var ok) || !ok.GetBoolean()) return "Não consegui descobrir a localização. Diga a cidade.";
            lat = ip.GetProperty("latitude").GetDouble();
            lon = ip.GetProperty("longitude").GetDouble();
            place = $"{ip.GetProperty("city").GetString()}, {ip.GetProperty("region").GetString()} (pela localização aproximada da internet)";
        }

        var inv = CultureInfo.InvariantCulture;
        var url = "https://api.open-meteo.com/v1/forecast?latitude=" + lat.ToString(inv) + "&longitude=" + lon.ToString(inv) +
                  "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&forecast_days=3&timezone=auto";
        var w = await client.GetFromJsonAsync<JsonElement>(url, ct);
        var cur = w.GetProperty("current");
        var sb = new StringBuilder();
        sb.AppendLine($"Local: {place}");
        sb.AppendLine($"Agora: {cur.GetProperty("temperature_2m").GetDouble():0}°C (sensação {cur.GetProperty("apparent_temperature").GetDouble():0}°C), " +
                      $"{Describe(cur.GetProperty("weather_code").GetInt32())}, umidade {cur.GetProperty("relative_humidity_2m").GetInt32()}%, vento {cur.GetProperty("wind_speed_10m").GetDouble():0} km/h.");
        var daily = w.GetProperty("daily");
        var names = new[] { "Hoje", "Amanhã", "Depois de amanhã" };
        for (var i = 0; i < daily.GetProperty("time").GetArrayLength() && i < 3; i++)
        {
            sb.AppendLine($"{names[i]}: {Describe(daily.GetProperty("weather_code")[i].GetInt32())}, " +
                          $"mín {daily.GetProperty("temperature_2m_min")[i].GetDouble():0}°C, máx {daily.GetProperty("temperature_2m_max")[i].GetDouble():0}°C, " +
                          $"chance de chuva {daily.GetProperty("precipitation_probability_max")[i]}%.");
        }
        return sb.ToString();
    }

    private static string Describe(int code) => code switch
    {
        0 => "céu limpo",
        1 or 2 => "parcialmente nublado",
        3 => "nublado",
        45 or 48 => "neblina",
        >= 51 and <= 57 => "garoa",
        >= 61 and <= 67 => "chuva",
        >= 71 and <= 77 => "neve",
        >= 80 and <= 82 => "pancadas de chuva",
        >= 95 => "tempestade",
        _ => "tempo variável"
    };

    // ── Web ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Busca por fontes abertas que permitem acesso automatizado: SearXNG próprio (se configurado),
    /// Wikipédia em português (fatos) e Google Notícias RSS (atualidades).
    /// </summary>
    private static async Task<string> WebSearchAsync(IHttpClientFactory http, string? searxngUrl, string query, CancellationToken ct)
    {
        var client = http.CreateClient("open-data");
        var sections = new List<string>();

        if (!string.IsNullOrWhiteSpace(searxngUrl))
        {
            try
            {
                var json = await client.GetFromJsonAsync<JsonElement>(
                    $"{searxngUrl.TrimEnd('/')}/search?q={Uri.EscapeDataString(query)}&format=json&language=pt-BR", ct);
                var items = json.GetProperty("results").EnumerateArray().Take(6)
                    .Select(r => $"- {r.GetProperty("title").GetString()}\n  {r.GetProperty("url").GetString()}\n  {(r.TryGetProperty("content", out var c) ? c.GetString() : "")}");
                sections.Add("WEB:\n" + string.Join("\n", items));
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { sections.Add($"(SearXNG indisponível: {ex.Message})"); }
        }

        var wiki = WikipediaAsync(client, query, ct);
        var news = NewsAsync(client, query, ct);
        foreach (var part in await Task.WhenAll(wiki, news))
            if (!string.IsNullOrWhiteSpace(part)) sections.Add(part);

        return sections.Count == 0 ? "Nenhum resultado encontrado." : string.Join("\n\n", sections);
    }

    private static async Task<string> WikipediaAsync(HttpClient client, string query, CancellationToken ct)
    {
        try
        {
            var search = await client.GetFromJsonAsync<JsonElement>(
                $"https://pt.wikipedia.org/w/rest.php/v1/search/page?q={Uri.EscapeDataString(query)}&limit=3", ct);
            var lines = new List<string>();
            foreach (var page in search.GetProperty("pages").EnumerateArray().Take(2))
            {
                var key = page.GetProperty("key").GetString();
                var summary = await client.GetFromJsonAsync<JsonElement>(
                    $"https://pt.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(key!)}", ct);
                var extract = summary.TryGetProperty("extract", out var e) ? e.GetString() : "";
                lines.Add($"- {page.GetProperty("title").GetString()}: {extract}\n  https://pt.wikipedia.org/wiki/{key}");
            }
            return lines.Count == 0 ? "" : "WIKIPÉDIA:\n" + string.Join("\n", lines);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return ""; }
    }

    private static async Task<string> NewsAsync(HttpClient client, string query, CancellationToken ct)
    {
        try
        {
            var rss = await client.GetStringAsync(
                $"https://news.google.com/rss/search?q={Uri.EscapeDataString(query)}&hl=pt-BR&gl=BR&ceid=BR:pt-419", ct);
            var doc = System.Xml.Linq.XDocument.Parse(rss);
            var items = doc.Descendants("item").Take(5).Select(i =>
                $"- {i.Element("title")?.Value} ({FormatDate(i.Element("pubDate")?.Value)})\n  {i.Element("link")?.Value}").ToList();
            return items.Count == 0 ? "" : "NOTÍCIAS:\n" + string.Join("\n", items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return ""; }
    }

    private static string FormatDate(string? rfc822) =>
        DateTimeOffset.TryParse(rfc822, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM/yyyy") : "";

    private static async Task<string> ReadWebpageAsync(IHttpClientFactory http, string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            return "Erro: URL inválida.";
        var client = http.CreateClient("web");
        var html = await client.GetStringAsync(uri, ct);
        html = Regex.Replace(html, "<(script|style|noscript|svg|nav|footer|header)[^>]*>.*?</\\1>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var title = Regex.Match(html, "<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Groups[1].Value;
        var text = StripTags(html);
        if (text.Length > 6000) text = text[..6000] + "…";
        return $"Título: {StripTags(title)}\n\n{text}";
    }

    private static string StripTags(string html)
    {
        var t = Regex.Replace(html, "<br\\s*/?>|</p>|</div>|</li>|</h\\d>", "\n", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, "<[^>]+>", " ");
        t = WebUtility.HtmlDecode(t);
        t = Regex.Replace(t, "[ \\t]+", " ");
        t = Regex.Replace(t, "\\s*\\n\\s*", "\n");
        return t.Trim();
    }
}
