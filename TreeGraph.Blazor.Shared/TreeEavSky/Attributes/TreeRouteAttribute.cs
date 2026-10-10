namespace TreeGraph.Blazor.Shared.TreeEavSky.Attributes;

/// <summary>
/// 声明泛型树 API 客户端使用的 URL 前缀。
///
/// 用途：<see cref="Services.TreeApiClient{T}"/> 用它替代 typeof(T).Name
/// 作为请求路径前缀，让 C# 类型改名不影响 URL 契约。
///
/// 示例：
///   [TreeRoute("string-tree-nodes")]
///   public class MyNode : ITreeNodeBase&lt;MyNode&gt; { ... }
///
/// 未标注时回退到 typeof(T).Name（向后兼容）。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class TreeRouteAttribute : Attribute
{
    /// <summary>URL 路径前缀（如 "StringTreeNode" 或 "string-tree-nodes"）。</summary>
    public string Route { get; }

    public TreeRouteAttribute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            throw new ArgumentException("Route 不能为空", nameof(route));

        Route = route;
    }
}
