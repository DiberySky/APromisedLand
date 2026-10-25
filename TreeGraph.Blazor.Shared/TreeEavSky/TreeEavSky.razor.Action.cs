using Microsoft.AspNetCore.Components;
using TreeGraph.Blazor.Shared.TreeEavSky.Contracts;
using TreeGraph.Blazor.Shared.TreeEavSky.Services;

namespace TreeGraph.Blazor.Shared.TreeEavSky;

public partial class TreeEavSky<TItem>
{
    // ========== 节点操作（编排逻辑已抽取至 TreeNodeActionOrchestrator，本类仅提供树内 UI 回调） ==========

    [Inject] private TreeNodeActionOrchestrator<TItem> NodeOps { get; set; } = default!;

    private TreeNodeActionUi<TItem> BuildActionUi() => new()
    {
        EditTemplate = EditTemplate,
        IsRoot = v => v.Id == RootId,
        GetParentNode = GetParentNode,
        FindLoadedNode = id => string.IsNullOrEmpty(id) ? null : _items?.FindTreeItem(id),
        LoadChildrenAsync = LoadChildrenAsync,
        RefreshNodeChildrenAsync = RefreshNodeChildrenAsync,
        RefreshTreeAsync = RefreshTreeAsync,
        ReloadAndExpandAsync = ReLoadingAsync,
    };

    private async Task ShowNodeActionsAsync(TreeNodeItem<TItem> node)
    {
        if (node.Value == null) return;

        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            ActionTemplate = ActionTemplate,
        };

        await NodeOps.OperateAsync(nodeTemplate, BuildActionUi(), _cts.Token);
    }

    /// <summary>执行单个节点动作（无宿主循环；共享编排实现见 <see cref="TreeNodeActionOrchestrator{TItem}"/>）。</summary>
    public async Task ExecuteNodeActionAsync(NodeAction action, TreeNodeItem<TItem> node)
        => await NodeOps.ExecuteAsync(action, node, BuildActionUi(), _cts.Token);

    #region 数据加载

    private async Task RefreshTreeAsync(CancellationToken ct = default)
    {
        await RefreshAsync(ct);
        SelectedValue = null;
    }

    #endregion
}
