using TreeGraph.Shared.NodeEav.Dtos;

namespace TreeGraph.Blazor.Shared.NodeEav.Services;

/// <summary>
/// 实体类型"显示名"解析器。
///
/// 缓存 EntityType → DisplayName 映射。
/// UI 中只显示 DisplayName（中文），EntityType（英文标识）仅用于 URL / API。
///
/// Scoped 生命周期：每个 Blazor Circuit 一份缓存。
/// </summary>
public class EntityTypeDisplayService
{
    private readonly EavApiClient _api;
    private Dictionary<string, EntityTypeSummaryDto>? _map;
    private SemaphoreSlim? _loadLock;
    private bool _loaded;

    public EntityTypeDisplayService(EavApiClient api)
    {
        _api = api;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        _loadLock ??= new SemaphoreSlim(1, 1);
        await _loadLock.WaitAsync();
        try
        {
            if (_loaded) return;

            var list = await _api.ListEntityTypesAsync()
                       ?? new List<EntityTypeSummaryDto>();
            _map = list
                .GroupBy(x => x.EntityType)
                .ToDictionary(g => g.Key, g => g.First());
            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public void Invalidate()
    {
        _loaded = false;
        _map = null;
    }

    /// <summary>
    /// 获取显示名。
    /// DisplayName 为空时回退到 EntityType。
    /// </summary>
    public string GetDisplayName(string? entityType)
    {
        if (string.IsNullOrEmpty(entityType)) return entityType ?? "";

        if (_map is not null
            && _map.TryGetValue(entityType, out var dto)
            && !string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            return dto.DisplayName;
        }

        return entityType;
    }

    /// <summary>从 SummaryDto 直接取显示名（不需要查表）。</summary>
    public static string GetDisplayName(EntityTypeSummaryDto dto)
        => string.IsNullOrWhiteSpace(dto.DisplayName)
            ? dto.EntityType
            : dto.DisplayName;
}
