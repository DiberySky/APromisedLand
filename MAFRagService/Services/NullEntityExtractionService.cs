using MAFRagService.Models;

namespace MAFRagService.Services;

/// <summary>
/// Entity 关闭时的空实现，返回空列表。
/// </summary>
public sealed class NullEntityExtractionService : IEntityExtractionService
{
    public Task<List<EntityInfo>> ExtractAsync(
        string text, string tenant, CancellationToken ct = default)
        => Task.FromResult(new List<EntityInfo>());
}