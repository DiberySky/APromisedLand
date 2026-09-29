using Microsoft.Extensions.AI;

namespace MafSampleApi.Services.Tools;

public interface IToolRegistry
{
    IReadOnlyList<ToolDescriptor> List();

    /// <summary>基础工具：进指纹，Agent 创建时绑定。</summary>
    IReadOnlyList<AIFunction> ResolveBase();

    /// <summary>高级工具：请求级动态注入，不进指纹。</summary>
    /// <remarks>
    /// - names 非空：只取名单内（不含基础工具，避免重复注入）
    /// - tags 非空：取包含任一 tag 的（不含基础工具）
    /// - 两者都空：返回 SafeByDefault=true 的非基础工具
    /// </remarks>
    IReadOnlyList<AIFunction> ResolveDynamic(
        IReadOnlyList<string>? names,
        IReadOnlyList<string>? tags);
}