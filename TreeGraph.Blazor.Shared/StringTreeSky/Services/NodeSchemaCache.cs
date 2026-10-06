using System.Collections.Concurrent;
using TreeGraph.Blazor.Shared.NodeEav.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Shared.StringTreeSky.Services;

/// <summary>
/// 按 EntityType 键控的 Schema 缓存。
///
/// 支持多棵树独立 Schema：
///   - "StringTreeNode"                 → 默认树
///   - "StringTreeNode:products"        → products 树
///   - "StringTreeNode:categories"      → categories 树
///
/// 每个 EntityType 首次拉取后缓存，并发保护。
/// Scoped 生命周期：每个 Blazor Circuit 一份。
/// </summary>
public class NodeSchemaCache
{
    private readonly EavApiClient _api;

    private readonly ConcurrentDictionary<string, IReadOnlyList<AttributeSchemaDto>> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public NodeSchemaCache(EavApiClient api)
    {
        _api = api;
    }

    /// <summary>指定 EntityType 是否已缓存。</summary>
    public bool IsLoaded(string entityType) => _cache.ContainsKey(entityType);

    /// <summary>
    /// 获取指定 EntityType 的 Schema。
    /// 首次拉取后缓存；后端返回 null 时缓存为空列表（避免重试风暴）。
    /// </summary>
    public async Task<IReadOnlyList<AttributeSchemaDto>> GetAsync(
        string entityType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            return Array.Empty<AttributeSchemaDto>();

        if (_cache.TryGetValue(entityType, out var cached))
            return cached;

        var gate = _locks.GetOrAdd(entityType, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(entityType, out cached))
                return cached;

            var raw = await _api.GetSchemaAsync(entityType, ct);
            var result = raw ?? Array.Empty<AttributeSchemaDto>();
            _cache[entityType] = result;
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>失效指定 EntityType 的缓存。</summary>
    public void Invalidate(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType)) return;
        _cache.TryRemove(entityType, out _);
    }

    /// <summary>失效全部缓存。</summary>
    public void InvalidateAll()
    {
        _cache.Clear();
    }
}
