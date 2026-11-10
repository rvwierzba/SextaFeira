using System.Text.Json;
using SextaFeira.Domain.Agent;

namespace SextaFeira.Infrastructure.Tools;

/// <summary>Ferramenta definida por um delegate + JSON Schema.</summary>
public sealed class FunctionTool : ITool
{
    private readonly Func<JsonElement, ToolContext, CancellationToken, Task<string>> _handler;

    public FunctionTool(string source, string name, string description, string parametersSchemaJson,
        Func<JsonElement, ToolContext, CancellationToken, Task<string>> handler)
    {
        Source = source;
        using var doc = JsonDocument.Parse(parametersSchemaJson);
        Spec = new ToolSpec(name, description, doc.RootElement.Clone());
        _handler = handler;
    }

    public ToolSpec Spec { get; }
    public string Source { get; }

    public Task<string> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct) =>
        _handler(arguments, context, ct);

    public const string NoParams = """{"type":"object","properties":{}}""";
}

public static class JsonArgs
{
    public static string? Str(this JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : v.ToString()
            : null;

    public static string Required(this JsonElement args, string name) =>
        args.Str(name) is { Length: > 0 } s ? s : throw new ArgumentException($"O parâmetro '{name}' é obrigatório.");

    public static int? Int(this JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        return int.TryParse(v.ToString(), out var j) ? j : null;
    }
}
