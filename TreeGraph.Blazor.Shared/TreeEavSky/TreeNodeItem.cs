using MudBlazor;

namespace TreeGraph.Blazor.Shared.TreeEavSky;

/// <summary>
/// TreeEavSky 本地树节点包装器（specs/node-sky §8.4 DiberyTree 解绑）：
/// 业务代码一律消费本类型，替代直接散用 MudBlazor 的 TreeItemData&lt;T&gt;。
///
/// 继承而非重新实现接口的原因：MudBlazor 9.11 的 MudTreeView.ServerData 签名锚定在
/// 具体类 TreeItemData&lt;T&gt;（Task 不变性使接口实现无法协变喂入）；
/// 继承后实例经协变可直达 ServerData/Items 边界，运行时实例始终为本类型，
/// ItemTemplate 处 (TreeNodeItem&lt;T&gt;)context 强转安全。
/// Equals/GetHashCode（按 Value 判等）、HasChildren、Expandable/Visible 默认值全部继承，行为零变化。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class TreeNodeItem<T> : TreeItemData<T>
{
}
