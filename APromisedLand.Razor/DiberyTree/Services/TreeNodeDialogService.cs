using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.DiberyTree.Category;
using APromisedLand.Razor.DiberyTree.Dialogs;
using APromisedLand.Razor.DiberyTree.Models;
using APromisedLand.Razor.DiberyTree.Pages;
using APromisedLand.Razor.Helper;
using APromisedLand.Shared.DiberyTree.Interfaces;
using MudBlazor;
using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Razor.DiberyTree.Attributes;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;

namespace APromisedLand.Razor.DiberyTree.Services;

/// <summary>
/// 树节点对话框服务
/// </summary>
/// <typeparam name="TItem">节点类型，必须实现 ITreeNode</typeparam>
public class TreeNodeDialogService<TItem>(
    IDialogService dialogService)
    where TItem : class, ITreeNodeBase<TItem>, new()
{
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
        NodeTemplate<TItem> nodeTemplate,
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

    #region 删除确认对话框

    public async Task<bool> ShowDeleteDialogAsync(
        TItem node,
        bool hasChildren = false,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "Node", node },
            { "HasChildren", hasChildren }
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.ExtraSmall,
            CloseButton = false,
            BackdropClick = false
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<TreeNodeDeleteDialog<TItem>>("确认删除", parameters, options);

        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    #endregion

    #region 父节点选择对话框

    public async Task<TItem?> ShowParentSelectDialogAsync(
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters();
        
        var options = (config ?? new DialogConfig
        {
            FullScreen = true,
        }).ToDialogOptions();
        
        var dialog = await dialogService.ShowAsync<CategoryTreeSelectDialog>("选择上级节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not TItem actionResult)
            return null;

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

    public async Task<ParentSelectResult<TItem>?> ShowParentSelectDialogAsync(
        TreeSky<TItem> treeSky,
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

    public async Task<SortResult<TItem>?> ShowSortDialogAsync(
        TreeSky<TItem> treeSky,
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
    
    #region 文件附件

    public async Task<NodeActionResult<TItem>?> ShowTreeFileDialogAsync(
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters();
        
        var options = (config ?? new DialogConfig
        {
            FullScreen = true,
        }).ToDialogOptions();
        
        var dialog = await dialogService.ShowAsync<TreeFileDialogPage<TItem>>("文件附件", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not NodeActionResult<TItem> actionResult)
            return null;

        return actionResult;
    }

    #endregion

    #region 图片附件

    public async Task<NodeActionResult<TItem>?> ShowTreeImageDialogAsync(
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters();
        
        var options = (config ?? new DialogConfig
        {
            FullScreen = true,
        }).ToDialogOptions();
        
        var dialog = await dialogService.ShowAsync<TreeImageDialogPage<TItem>>("图片附件", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not NodeActionResult<TItem> actionResult)
            return null;

        return actionResult;
    }

    #endregion
    
    #region 视频附件

    public async Task<NodeActionResult<TItem>?> ShowTreeVideoDialogAsync(
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters();
        
        var options = (config ?? new DialogConfig
        {
            FullScreen = true,
        }).ToDialogOptions();
        
        var dialog = await dialogService.ShowAsync<TreeVideoDialogPage<TItem>>("视频附件", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not NodeActionResult<TItem> actionResult)
            return null;

        return actionResult;
    }

    #endregion
    
    #region 位置附件

    public async Task<NodeActionResult<TItem>?> ShowTreeLocationDialogAsync(
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters();
        
        var options = (config ?? new DialogConfig
        {
            FullScreen = true,
        }).ToDialogOptions();
        
        var dialog = await dialogService.ShowAsync<TreeLocationDialogPage<TItem>>("位置附件", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not NodeActionResult<TItem> actionResult)
            return null;

        return actionResult;
    }

    #endregion

    // ==================== 新增：节点属性管理 ====================

    #region 节点属性管理

    /// <summary>
    /// 显示节点属性管理对话框（查看/添加/删除属性）
    /// </summary>
    /// <param name="nodeId">节点 ID</param>
    /// <param name="config">对话框配置</param>
    /// <returns>是否进行了修改（关闭时返回 true，取消返回 false）</returns>
    public async Task<bool> ShowNodeAttributesDialogAsync(
        string nodeId,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { nameof(NodeAttributesDialog<TItem>.NodeId), nodeId }
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<NodeAttributesDialog<TItem>>(
            "节点属性", parameters, options);

        var result = await dialog.Result;
        // 只要不是取消就返回 true（表示可能已修改）
        return result is not { Canceled: true };
    }

    #endregion
}