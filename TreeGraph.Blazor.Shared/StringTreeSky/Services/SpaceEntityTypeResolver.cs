using TreeGraph.StringTree.Contracts;

namespace TreeGraph.Blazor.Shared.StringTreeSky.Services;

/// <summary>
/// 把 EntityType 解析为人类可读的展示信息。
///
/// 用途：EAV 元数据页按空间分组时，把 "StringTreeNode:{guid}"
/// 翻译成"空间名"。Scoped 生命周期（与 Circuit 对齐）。
/// </summary>
public class SpaceEntityTypeResolver
{
    private readonly ISpaceClient _spaceClient;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Dictionary<string, SpaceDto> _spaceByEntityType = new();
    private bool _loaded;

    public SpaceEntityTypeResolver(ISpaceClient spaceClient)
    {
        _spaceClient = spaceClient;
    }

    public bool IsLoaded => _loaded;

    /// <summary>首次拉取空间列表，建字典。并发保护。</summary>
    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loaded) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_loaded) return;

            var spaces = await _spaceClient.ListSpacesAsync(ct)
                         ?? new List<SpaceDto>();

            _spaceByEntityType = spaces
                .Where(s => !string.IsNullOrEmpty(s.EntityType))
                .GroupBy(s => s.EntityType)
                .ToDictionary(g => g.Key, g => g.First());

            _loaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>失效缓存（新建/删除空间后调用）。</summary>
    public void Invalidate()
    {
        _loaded = false;
        _spaceByEntityType.Clear();
    }

    /// <summary>把 EntityType 解析为可展示信息。</summary>
    public ResolvedEntityType Resolve(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return new ResolvedEntityType(
                entityType ?? "", entityType ?? "", null,
                EntityTypeKind.Other, null);
        }

        // 1. 共享默认
        if (entityType == StringTreeEntityTypes.Node)
        {
            return new ResolvedEntityType(
                entityType,
                "共享默认属性集",
                "所有「共享」空间共用",
                EntityTypeKind.Shared,
                null);
        }

        // 2. 独立空间（匹配成功）
        if (_spaceByEntityType.TryGetValue(entityType, out var space))
        {
            return new ResolvedEntityType(
                entityType,
                space.Name,
                "独立空间属性集",
                EntityTypeKind.SpaceOwned,
                space);
        }

        // 3. StringTreeNode:xxx 但匹配不到空间 → 孤立
        if (entityType.StartsWith(StringTreeEntityTypes.Node + ":", StringComparison.Ordinal))
        {
            return new ResolvedEntityType(
                entityType,
                "未知空间",
                "对应空间不存在或已删除",
                EntityTypeKind.Orphan,
                null);
        }

        // 4. 其他 EntityType
        return new ResolvedEntityType(
            entityType,
            entityType,
            "其他实体类型",
            EntityTypeKind.Other,
            null);
    }

    public bool TryGetSpaceName(string entityType, out string? spaceName)
    {
        spaceName = null;
        if (_spaceByEntityType.TryGetValue(entityType, out var space))
        {
            spaceName = space.Name;
            return true;
        }
        return false;
    }
}

public record ResolvedEntityType(
    string EntityType,
    string DisplayName,
    string? SubLabel,
    EntityTypeKind Kind,
    SpaceDto? RelatedSpace);

public enum EntityTypeKind
{
    /// <summary>共享默认（"StringTreeNode"）。</summary>
    Shared,

    /// <summary>独立空间（"StringTreeNode:{guid}" 且匹配到空间）。</summary>
    SpaceOwned,

    /// <summary>孤立（"StringTreeNode:{guid}" 但空间已删）。</summary>
    Orphan,

    /// <summary>其他 EntityType。</summary>
    Other
}
