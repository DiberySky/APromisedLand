using TreeGraph.TreeSky.Models;
using TreeGraph.TreeSky.Services;

namespace TreeGraph.Blazor.Services.DemoTree;

/// <summary>
/// 字符串演示节点的 <see cref="ITreeClientService{TTree}"/> 实现：直接读内存存储，无 HTTP。
/// </summary>
public class DemoTreeClientService(InMemoryTreeStore store) : ITreeClientService<StringTreeNode>
{
    public string Title { get; set; } = "字符串节点演示树";

    /// <summary>关闭“新页面打开”：演示中点击目录不触发 forceLoad 整页跳转。</summary>
    public bool NewPageShow { get; set; }

    /// <summary>移动/选择对话框中只能选中叶子（与源计量单位树语义一致）。</summary>
    public bool SelectLeaf { get; set; } = true;

    public Task<IReadOnlyList<TreeNodeDto<StringTreeNode>>> LoadInitialDataAsync(string? rootId)
        => Task.FromResult(store.GetRoots(rootId));

    public Task<IReadOnlyList<TreeNodeDto<StringTreeNode>>> LoadChildrenAsync(StringTreeNode? parent = null)
        => Task.FromResult(parent == null ? store.GetRoots() : store.GetChildren(parent.Id));

    public Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId)
        => Task.FromResult<List<string>?>([.. store.GetAncestorPath(nodeId)]);
}

