using System.Text.Json;
using ModelContextProtocol.Authentication;

namespace SextaFeira.Infrastructure.Mcp;

/// <summary>Guarda os tokens OAuth de um servidor MCP em disco para não pedir login a cada reinício.</summary>
internal sealed class FileTokenCache : ITokenCache
{
    private readonly string _path;

    public FileTokenCache(string path) => _path = path;

    public async ValueTask<TokenContainer?> GetTokensAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return null;
        try
        {
            await using var fs = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<TokenContainer>(fs, cancellationToken: cancellationToken);
        }
        catch (JsonException) { return null; }
    }

    public async ValueTask StoreTokensAsync(TokenContainer tokens, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var fs = File.Create(_path);
        await JsonSerializer.SerializeAsync(fs, tokens, cancellationToken: cancellationToken);
    }
}
