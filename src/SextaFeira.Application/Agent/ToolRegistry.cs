using SextaFeira.Domain.Agent;

namespace SextaFeira.Application.Agent;

/// <summary>Junta ferramentas embutidas e as descobertas dinamicamente (servidores MCP).</summary>
public sealed class ToolRegistry
{
    private readonly IEnumerable<ITool> _builtin;
    private readonly IEnumerable<IToolProvider> _providers;
    private readonly ISettingsStore _settings;

    public ToolRegistry(IEnumerable<ITool> builtin, IEnumerable<IToolProvider> providers, ISettingsStore settings)
    {
        _builtin = builtin;
        _providers = providers;
        _settings = settings;
    }

    public IReadOnlyList<ITool> GetAll()
    {
        var selfCode = _settings.Current.SelfCode.Enabled;
        var all = _builtin
            .Where(t => selfCode || t.Source != "code")
            .Concat(_providers.SelectMany(p => p.GetTools()));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ITool>();
        foreach (var t in all)
            if (seen.Add(t.Spec.Name)) result.Add(t);
        return result;
    }

    public ITool? Find(string name) =>
        GetAll().FirstOrDefault(t => string.Equals(t.Spec.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Escolhe a engine ativa a partir das configurações; o Ollama local é sempre o reserva.</summary>
public sealed class EngineRouter
{
    private readonly IReadOnlyList<IChatEngine> _engines;
    private readonly ISettingsStore _settings;

    public EngineRouter(IEnumerable<IChatEngine> engines, ISettingsStore settings)
    {
        _engines = engines.ToList();
        _settings = settings;
    }

    public IReadOnlyList<IChatEngine> All => _engines;

    public IChatEngine Local => _engines.First(e => e.IsLocal);

    public IChatEngine ResolveActive()
    {
        var id = _settings.Current.ActiveEngine;
        return _engines.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Local;
    }

    public IChatEngine? Find(string id) =>
        _engines.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
}
