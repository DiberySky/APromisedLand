using TreeGraph.Blazor.Shared.NodeEavSky.Services;

namespace TreeGraph.Blazor.Shared.StringTreeSky.Services;

/// <summary>
/// 按 EntityType 统计属性数量。
///
/// 一次拉取全部属性定义，按 EntityType 分组计数。
/// Scoped 生命周期（与 Circuit 对齐）。
/// </summary>
public class SpaceAttributeStatsService
{
    private readonly EavApiClient _eavApi;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Dictionary<string, int>? _counts;

    public SpaceAttributeStatsService(EavApiClient eavApi)
    {
        _eavApi = eavApi;
    }

    /// <summary>
    /// 获取指定 EntityType 的属性数量。
    /// 首次调用拉取所有属性并缓存。
    /// </summary>
    public async Task<int> GetCountAsync(string entityType, CancellationToken ct = default)
    {
        var map = await EnsureLoadedAsync(ct);
        return map.TryGetValue(entityType, out var n) ? n : 0;
    }

    /// <summary>
    /// 批量获取。
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> GetCountsAsync(
        IEnumerable<string> entityTypes, CancellationToken ct = default)
    {
        var map = await EnsureLoadedAsync(ct);
        var wanted = entityTypes.Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();

        return wanted.ToDictionary(
            t => t,
            t => map.TryGetValue(t, out var n) ? n : 0);
    }

    public void Invalidate()
    {
        _counts = null;
    }

    private async Task<Dictionary<string, int>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_counts is not null) return _counts;

        await _lock.WaitAsync(ct);
        try
        {
            if (_counts is not null) return _counts;

            var list = await _eavApi.ListAttributesAsync(includeDeleted: false, ct: ct)
                       ?? new List<TreeGraph.Shared.NodeEavSky.Dtos.AttributeDetailDto>();

            _counts = list
                .GroupBy(a => a.EntityType ?? "")
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToDictionary(g => g.Key, g => g.Count());

            return _counts;
        }
        finally
        {
            _lock.Release();
        }
    }
}
