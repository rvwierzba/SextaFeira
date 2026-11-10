namespace SextaFeira.Infrastructure.Storage;

/// <summary>Pastas de dados locais (fora do git): data/settings.json, data/memory.json, data/models, data/certs...</summary>
public static class DataPaths
{
    public static string Root { get; private set; } = Path.Combine(AppContext.BaseDirectory, "data");

    public static void Initialize(string root)
    {
        Root = root;
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Models);
        Directory.CreateDirectory(Certs);
    }

    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Memory => Path.Combine(Root, "memory.json");
    public static string McpConfig => Path.Combine(Root, "mcp.json");
    public static string McpTokens => Path.Combine(Root, "mcp-tokens");
    public static string Models => Path.Combine(Root, "models");
    public static string Certs => Path.Combine(Root, "certs");

    /// <summary>Encontra a raiz do repositório (pasta que contém SextaFeira.sln), subindo a partir de <paramref name="start"/>.</summary>
    public static string? FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SextaFeira.sln"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
