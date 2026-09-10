// 桩代码：MAF 记忆存储类型

namespace MAFRagService.Stubs.MAF;

public interface IMemoryStore
{
    Task<IEnumerable<MemoryEntry>> QueryAsync(MemoryQuery query, CancellationToken ct);
    Task AddMemoryAsync(MemoryEntry entry, CancellationToken ct);
}

public class MemoryQuery
{
    public string Text { get; set; } = string.Empty;
    public string? Tenant { get; set; }
    public int Limit { get; set; } = 5;
}

public class MemoryEntry
{
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
