using System.Diagnostics;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// Scoped 服务：每次 HTTP 请求内共享一个实例，记录 LLM 调用的每个工具的详情。
/// </summary>
public sealed class ToolCallContext
{
    private readonly List<ToolCallRecord> _records = new();

    public IReadOnlyList<ToolCallRecord> Records => _records;

    public void Reset() => _records.Clear();

    /// <summary>开始一次工具调用记录，返回可写的 record。</summary>
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

    internal Stopwatch? Stopwatch { get; set; }

    public void Complete(string result)
    {
        Result = result;
        Success = true;
        ElapsedMs = Stopwatch?.ElapsedMilliseconds ?? 0;
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