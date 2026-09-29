using Microsoft.Extensions.AI;

namespace MafRagApi.Services.Tools;

/// <summary>工具元数据 + 执行体。</summary>
public sealed record ToolDescriptor
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required AIFunction Function { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    /// <summary>默认是否启用（false 需请求级显式启用）。</summary>
    public bool SafeByDefault { get; init; } = true;
    /// <summary>是否为基础工具（进 Agent 指纹，会话级绑定）。</summary>
    public bool IsBase { get; init; } = false;
}