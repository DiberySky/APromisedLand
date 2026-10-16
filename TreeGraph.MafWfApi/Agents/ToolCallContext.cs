using System.Diagnostics;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.MafWfApi.Agents;

/// <summary>
/// Scoped 服务：每次 HTTP 请求内共享一个实例，记录 LLM 调用的每个工具的详情。
/// </summary>
public sealed class ToolCallContext
{
    private readonly List<ToolCallRecord> _records = new();

    public IReadOnlyList<ToolCallRecord> Records => _records;

    /// <summary>
    /// SemanticSearchNodes 工具最近一次命中的结果集。
    /// 非 null 时 GraphAgentService 流式管道会补发一个 SearchResults chunk。
    /// </summary>
    public IReadOnlyList<SemanticSearchItemDto>? SearchResults { get; set; }

    public void Reset()
    {
        _records.Clear();
        SearchResults = null;
    }

    public ToolCallRecord BeginCall(string toolName, string arguments)
    {
        var record = new ToolCallRecord
        {
            ToolName = toolName,
            Arguments = arguments,
            StartTime = DateTime.UtcNow,
            Stopwatch = Stopwatch.StartNew()
        };
        _records.Add(record);
        return record;
    }
}

/// <summary>单次工具调用的完整记录。</summary>
public sealed class ToolCallRecord
{
    public string ToolName { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string? Result { get; set; }
    public long ElapsedMs { get; set; }
    public DateTime StartTime { get; set; }
    public bool Success { get; set; }
    /// <summary>★ 新增：是否来自缓存。</summary>
    public bool FromCache { get; set; }

    internal Stopwatch? Stopwatch { get; set; }

    public void Complete(string result, bool fromCache = false)
    {
        Result = result;
        Success = true;
        FromCache = fromCache;
        ElapsedMs = fromCache ? 0 : (Stopwatch?.ElapsedMilliseconds ?? 0);
        Stopwatch?.Stop();
    }

    public void Fail(string error)
    {
        Result = $"【异常】{error}";
        Success = false;
        ElapsedMs = Stopwatch?.ElapsedMilliseconds ?? 0;
        Stopwatch?.Stop();
    }
}