using MAFRagService.Stubs.MAF;

namespace MAFRagService.Memory;

/// <summary>
/// Graph 关闭时的空记忆存储，保证 OrchestratorAgent 构造不失败。
/// </summary>
public sealed class NullMemoryStore : IMemoryStore
{
    public Task<IEnumerable<MemoryEntry>> QueryAsync(MemoryQuery query, CancellationToken ct)
        => Task.FromResult<IEnumerable<MemoryEntry>>(Array.Empty<MemoryEntry>());

    public Task AddMemoryAsync(MemoryEntry entry, CancellationToken ct)
        => Task.CompletedTask;
}