using TreeGraph.Blazor.Shared.Trees.TreeSky.Models;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Services;

namespace TreeGraph.Blazor.Services.DemoTree;

/// <summary>
/// 字符串节点的 <see cref="ITreeClientService{TTree}"/> 实现：
/// 通过 <see cref="DiberyTreeApiClient{T}"/> 访问 TreeGraph.Api 的 StringTreeNodeController。
/// </summary>
public class StringTreeClientService(DiberyTreeApiClient<StringTreeNode> api) : ITreeClientService<StringTreeNode>
{
    public string Title { get; set; } = "字符串节点演示树";

    /// <summary>关闭"新页面打开"：演示中点击目录不触发 forceLoad 整页跳转。</summary>
    public bool NewPageShow { get; set; }

    /// <summary>移动/选择对话框中只能选中叶子（与源计量单位树语义一致）。</summary>
    public bool SelectLeaf { get; set; } = true;

    public async Task<IReadOnlyList<TreeNodeDto<StringTreeNode>>> LoadInitialDataAsync(
        string? rootId, CancellationToken ct = default)
        => await api.GetRootNodesAsync(rootId, ct);

    public async Task<IReadOnlyList<TreeNodeDto<StringTreeNode>>> LoadChildrenAsync(
        StringTreeNode? parent = null, CancellationToken ct = default)
        => parent == null
            ? await api.GetRootNodesAsync(null, ct)
            : await api.GetChildrenAsync(parent.Id, ct);

    public async Task<List<string>?> GetAncestorPathFromApiAsync(
        string nodeId, CancellationToken ct = default)
        => [.. await api.GetAncestorPathAsync(nodeId, ct)];
}
