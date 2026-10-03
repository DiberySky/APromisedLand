using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;

namespace TreeGraph.Api.Tests.Fixtures;

public sealed class InMemoryCompositeTypeCache : ICompositeTypeCache
{
    private readonly Dictionary<string, CompositeTypeDefinition> _types = new();

    public void Add(CompositeTypeDefinition type) => _types[type.CompositeTypeId] = type;
    public void Clear() => _types.Clear();

    public CompositeTypeDefinition GetType(string id)
        => _types.TryGetValue(id, out var t)
            ? t
            : throw new KeyNotFoundException($"CompositeType {id} 不存在");

    public void Invalidate(string id) => _types.Remove(id);
}

public sealed class InMemoryUnitCache : IUnitCache
{
    private readonly List<Unit> _units = new();

    public void Add(Unit u) => _units.Add(u);
    public void Clear() => _units.Clear();

    public Unit Get(Guid id)
        => _units.FirstOrDefault(u => u.Id == id)
           ?? throw new InvalidOperationException($"Unit {id} 不存在");

    public IReadOnlyList<Unit> GetAll() => _units;
    public IReadOnlyList<Unit> GetByCategory(string category)
        => _units.Where(u => u.Category == category).ToList();
    public Unit? GetBaseUnit(string category)
        => _units.FirstOrDefault(u => u.Category == category && u.IsBaseUnit);
    public void Invalidate() { }
}

public sealed class InMemoryOptionSetCache : IOptionSetCache
{
    private readonly Dictionary<string, OptionSet> _sets = new();

    public void Add(OptionSet s) => _sets[s.OptionSetId] = s;
    public void Clear() => _sets.Clear();

    public OptionSet GetSet(string id)
        => _sets.TryGetValue(id, out var s)
            ? s
            : throw new KeyNotFoundException($"OptionSet {id} 不存在");

    public List<OptionSet> GetAll(string? entityType = null, bool includeDeleted = false)
    {
        var query = _sets.Values.AsEnumerable();
        if (!includeDeleted)
            query = query.Where(s => !s.IsDeleted);
        if (entityType is not null)
            query = query.Where(s => s.EntityType == entityType || s.EntityType == "Shared");
        return query.ToList();
    }

    public void Invalidate(string id) => _sets.Remove(id);
}
