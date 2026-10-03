using MudBlazor;
using MudBlazor.Extensions;
using TreeGraph.TreeSky.Components.Base;
using TreeGraph.TreeSky.Components.Nodes;
using TreeGraph.TreeSky.Models;

namespace TreeGraph.TreeSky;

/// <summary>
/// 树节点对话框服务（精简移植版：保留 TreeSky 组件自身使用的对话框，
/// 不含文件/图片/视频/位置附件页与节点属性子系统）。
/// </summary>
/// <typeparam name="TItem">节点类型，必须实现 ITreeNodeBase</typeparam>
public class TreeNodeDialogService<TItem>(
    IDialogService dialogService,
    BlazorService blazorService)
    where TItem : class, ITreeNodeBase<TItem>, new()
{
    public async Task<TItem?> ShowDialogPageAsync()
    {
        var parameters = new DialogParameters<TreeDialogPageSky<TItem>>
        {
            { x => x.TitleToolBar, "" },
            { x => x.Title, "注册" },
        };

        var options = blazorService.DialogOptions;

        var dialog = await dialogService.ShowExAsync<TreeDialogPageSky<TItem>>("注册", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not TItem actionResult)
            return null;

        return actionResult;
    }

    public async Task<TItem?> ShowTreeSelectDialogAsync()
    {
        var parameters = new DialogParameters<TreeSelectDialogSky<TItem>>
        {
            { x => x.OriginalNode, null },
            { x => x.TitleToolBar, "请选择 =>" },
            { x => x.Title, "" },
        };

        var options = blazorService.DialogOptions;

        var dialog = await dialogService.ShowExAsync<TreeSelectDialogSky<TItem>>("选择节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not TItem actionResult)
            return null;

        return actionResult;
    }

    #region 操作选择对话框

    public async Task<NodeActionResult<TItem>?> ShowActionsDialogAsync(
        NodeTemplate<TItem> nodeTemplate,
        TItem? parentNode = null,
        bool isBoot = false,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "NodeTemplate", nodeTemplate },
            { "ParentNode", parentNode },
            { "IsBoot", isBoot }
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.Small,
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<TreeNodeActionsDialog<TItem>>("节点操作", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not NodeActionResult<TItem> actionResult)
            return null;

        return actionResult;
    }

    #endregion

    #region 查看详情对话框

    public async Task<bool> ShowViewDialogAsync(
        TItem node,
        TItem? parentNode = null,
        string? createdAt = null,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "Node", node },
            { "ParentNode", parentNode },
            { "CreatedAt", createdAt }
        };

        var options = (config ?? new DialogConfig { MaxWidth = MaxWidth.Small }).ToDialogOptions();
        var dialog = await dialogService.ShowAsync<TreeNodeViewDialog<TItem>>("节点详情", parameters, options);

        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    #endregion

    #region 编辑/创建对话框

    public async Task<TItem?> ShowCreateDialogAsync(
        NodeTemplate<TItem>? nodeTemplate = null,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "NodeTemplate", nodeTemplate },
            { "IsCreate", true }
        };

        var options = (config ?? new DialogConfig { MaxWidth = MaxWidth.Small }).ToDialogOptions();
        var dialog = await dialogService.ShowAsync<TreeNodeEditDialog<TItem>>("创建节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not TItem formModel)
            return null;

        return formModel;
    }

    public async Task<TItem?> ShowEditDialogAsync(
        NodeTemplate<TItem> nodeTemplate,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "NodeTemplate", nodeTemplate },
            { "IsCreate", false }
        };

        var options = (config ?? new DialogConfig { MaxWidth = MaxWidth.Small }).ToDialogOptions();
        var dialog = await dialogService.ShowAsync<TreeNodeEditDialog<TItem>>("编辑节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not TItem formModel)
            return null;

        return formModel;
    }

    #endregion

    #region 父节点选择对话框

    public async Task<TItem?> ShowParentSelectDialogAsync(
        ITreeItemData<TItem>? node = null)
    {
        var parameters = new DialogParameters<TreeSelectDialogSky<TItem>>
        {
            { x => x.OriginalNode, node },
            { x => x.TitleToolBar, node?.Value?.Parent?.Text() + " =>" },
            { x => x.Title, node?.Value?.Text() },
        };

        var options = blazorService.DialogOptions;
        var dialog = await dialogService.ShowExAsync<TreeSelectDialogSky<TItem>>("选择上级节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not TItem actionResult)
            return null;

        if (actionResult.Id == node?.Value?.ParentId) return null;

        return actionResult;
    }

    public async Task<ParentSelectResult<TItem>?> ShowParentSelectDialogAsync(
        List<TItem> treeItems,
        TItem? currentNode = null,
        TItem? currentParent = null,
        bool allowRoot = true,
        Func<TItem, bool>? canSelectNode = null,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "TreeItems", treeItems },
            { "CurrentNode", currentNode },
            { "CurrentParent", currentParent },
            { "AllowRootSelection", allowRoot },
            { "CanSelectNode", canSelectNode }
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.Medium,
            CloseButton = true
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<TreeNodeParentSelectDialog<TItem>>(
            "选择父节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not ParentSelectResult<TItem> selectResult)
            return null;

        return selectResult;
    }

    public async Task<ParentSelectResult<TItem>?> ShowParentSelectDialogAsync(Components.Base.TreeSky<TItem> treeSky,
        TItem? currentNode = null,
        TItem? currentParent = null,
        bool allowRoot = true,
        DialogConfig? config = null)
    {
        var allItems = await treeSky.GetAllNodesAsync();
        return await ShowParentSelectDialogAsync(
            allItems, currentNode, currentParent, allowRoot, null, config);
    }

    #endregion

    #region 拖拽排序对话框

    public async Task<List<TItem>?> ShowSortDialogAsync(
        ITreeItemData<TItem> node,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "Node", node },
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.Small,
            CloseButton = false,
            BackdropClick = false
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<TreeNodeSortDialog<TItem>>(
            "拖拽排序", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not List<TItem> sortResult)
            return null;

        return sortResult;
    }

    public async Task<SortResult<TItem>?> ShowSortDialogAsync(
        List<TItem> treeItems,
        bool allowHierarchyChange = true,
        int maxDepth = 10,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "TreeItems", treeItems },
            { "AllowHierarchyChange", allowHierarchyChange },
            { "MaxDepth", maxDepth }
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.Large,
            CloseButton = true,
            BackdropClick = false
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<TreeNodeSortDialog<TItem>>(
            "拖拽排序", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not SortResult<TItem> sortResult)
            return null;

        return sortResult;
    }

    public async Task<SortResult<TItem>?> ShowSortDialogAsync(Components.Base.TreeSky<TItem> treeSky,
        bool allowHierarchyChange = true,
        int maxDepth = 10,
        DialogConfig? config = null)
    {
        var allItems = await treeSky.GetAllNodesAsync();
        return await ShowSortDialogAsync(allItems, allowHierarchyChange, maxDepth, config);
    }

    #endregion

    #region 便捷方法

    public async Task<NodeOperationOutcome<TItem>?> ExecuteNodeOperationAsync(
        NodeTemplate<TItem> nodeTemplate,
        TItem? parentNode = null,
        bool isLeaf = false)
    {
        var actionResult = await ShowActionsDialogAsync(nodeTemplate, parentNode, isLeaf);
        if (actionResult == null)
            return null;

        return new NodeOperationOutcome<TItem>
        {
            Action = actionResult.Action,
            Node = actionResult.Node
        };
    }

    #endregion
}
