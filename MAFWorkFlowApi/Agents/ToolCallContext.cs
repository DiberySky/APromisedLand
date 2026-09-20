namespace MAFWorkFlowApi.Agents;

/// <summary>
/// Scoped 服务：每次 HTTP 请求内共享一个实例，用于记录 LLM 调用了哪些工具。
/// 比 AsyncLocal 更可靠（MAF 内部异步链路不会丢失作用域）。
/// </summary>
public sealed class ToolCallContext
{
    private readonly List<string> _invoked = new();

    public IReadOnlyList<string> InvokedTools => _invoked;

    public void Record(string toolName)
    {
        if (!string.IsNullOrWhiteSpace(toolName))
            _invoked.Add(toolName);
    }

    public void Reset() => _invoked.Clear();
}