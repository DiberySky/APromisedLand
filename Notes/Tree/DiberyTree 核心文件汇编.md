# DiberyTree 核心文件汇编

> 来源项目：`DiberyTreeService` / `APromisedLand.Razor` / `APromisedLand.MauiBlazor` / `APromisedLand.Shared`
> 生成日期：2026-10-03
> 说明：按 14 个目标项汇编，代码为仓库当前内容逐字收录。

## 目录

1. [ITreeNavigationHistoryService（实现 + 注入 + 调用）](#1-itreenavigationhistoryservice实现--注入--调用)
2. [DialogConfig + ToDialogOptions() 扩展 + TreeNodeDialogService](#2-dialogconfig--todialogoptions-扩展--treenodedialogservice)
3. [ApiResponse&lt;T&gt; + DiberyTreeApiClient](#3-apiresponset--diberytreeapiclient)
4. [TreeQueryParams（QueryNodesAsync 入参）](#4-treequeryparamsquerynodesasync-入参)
5. [NodeActionResult&lt;TItem&gt;](#5-nodeactionresulttitem)
6. [ParentSelectResult&lt;TItem&gt;](#6-parentselectresulttitem)
7. [SortResult&lt;TItem&gt;](#7-sortresulttitem)
8. [NodeOperationOutcome&lt;TItem&gt;](#8-nodeoperationoutcometitem)
9. [MessageDialog（Razor 组件）+ MessageService](#9-messagedialograzor-组件--messageservice)
10. [MudBlazor.Extensions：ShowExAsync / DialogOptionsEx 用法](#10-mudblazorextensionsshowexasync--dialogoptionsex-用法)
11. [UnitTree 模型 + BlazorService.UnitTree](#11-unittree-模型--blazorserviceunittree)
12. [UnitTreeClientService](#12-unittreeclientservice)
13. [CategoryTreeDialogPage + BlazorService.CategoryTree](#13-categorytreedialogpage--blazorservicecategorytree)
14. [UnitTreeDialogPage](#14-unittreedialogpage)

---

## 1. ITreeNavigationHistoryService（实现 + 注入 + 调用）

- 命名空间：`APromisedLand.Razor.DiberyTree.Navigation`
- 项目：`APromisedLand.Razor`
- 文件：
  - `DiberyTree/Navigation/ITreeNavigationHistoryService.cs`
  - `DiberyTree/Navigation/TreeNavigationHistoryService.cs`
  - `DiberyTree/Navigation/HistoryEntry.cs`
- 注入点：`DiberyTree/Base/TreeSky.razor`（`@inject ITreeNavigationHistoryService History`）、`DiberyTree/Base/TreePageSky.razor`
- 调用点：`DiberyTree/Base/TreeSky.razor.Node.cs`（`History.Push`）、`TreePageSky.razor`（`Peek/Pop/Clear`）

### 1.1 ITreeNavigationHistoryService.cs

```csharp
namespace APromisedLand.Razor.DiberyTree.Navigation;

public interface ITreeNavigationHistoryService
{
    void Push(string url, string? rootId = null, string? clickNodeId = null);
    HistoryEntry? Pop();
    HistoryEntry? Peek();
    HistoryEntry? PopReturnUrlOrDefault(string defaultUrl);
    void Clear();
    bool CanGoBack { get; }
    IReadOnlyCollection<HistoryEntry> Stack { get; }
}
```

### 1.2 TreeNavigationHistoryService.cs（实现类）

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Razor.DiberyTree.Navigation;

public class TreeNavigationHistoryService : ITreeNavigationHistoryService
{
    private readonly Stack<HistoryEntry> _stack = new();

    public IReadOnlyCollection<HistoryEntry> Stack => _stack.ToList().AsReadOnly();
    public bool CanGoBack => _stack.Count > 0;

    /// <summary>
    /// 打开新页前调用，保存当前位置
    /// </summary>
    public void Push(string url, string? rootId = null, string? clickNodeId = null)
    {
        var entry = new HistoryEntry
        {
            Url = url,
            RootId = rootId,
            ClickNodeId = clickNodeId,
        };
        
        _stack.Push(entry);
    }

    /// <summary>
    /// 返回上一页（弹出并返回）
    /// </summary>
    public HistoryEntry? Pop()
    {
        if (_stack.Count == 0) return null;
        
        var entry = _stack.Pop();
        return entry;
    }

    public HistoryEntry? Peek() => _stack.Count > 0 ? _stack.Peek() : null;

    /// <summary>
    /// 弹出并导航返回，或跳转到默认页
    /// </summary>
    public HistoryEntry? PopReturnUrlOrDefault(string defaultUrl)
    {
        var entry = Pop();
        if (entry != null)
        {
            // navigation.NavigateTo(entry.Url, forceLoad: true);
            return entry;
        }
        
        // navigation.NavigateTo(defaultUrl, forceLoad: true);
        return null;
    }

    public void Clear() => _stack.Clear();
}
```

### 1.3 HistoryEntry.cs

```csharp
namespace APromisedLand.Razor.DiberyTree.Navigation;

public class HistoryEntry
{
    public string Url { get; set; } = string.Empty;
    public string? RootId { get; set; }
    public string? ClickNodeId { get; set; }

    // public Dictionary<string, object?> State { get; set; } = new();
    // public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now; 
}
```

### 1.4 注入点：TreeSky.razor（节选头部注入区）

```razor
@* TreeSky.razor *@
@namespace APromisedLand.Razor.DiberyTree.Base
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>, new()

@inject DiberyTreeApiClient<TItem> ApiClient
@inject ITreeNavigationHistoryService History
@inject IDialogService DialogService
@inject ISnackbar Snackbar
@inject BlazorService BlazorService
@inject ITreeClientService<TItem> ClientService
@inject TreeNodeDialogService<TItem> NodeDialogSvc
@inject MessageService Message
```

### 1.5 调用点：TreeSky.razor.Node.cs（ClickItemText 中的 Push）

```csharp
// ========== 节点点击与导航 ==========
private async Task ClickItemText(ITreeItemData<TItem> node)
{
    SelectedValue = node.Value;
    _ = SelectedValueChanged.InvokeAsync(node.Value);

    if (IsSelectDialog)
    {
        // 触发外部回调，由调用方决定导航行为
        await OnClickItemText.InvokeAsync(node);

        return;
    }

    if (node.Value!.Id == RootId) return;
    if (!ClientService.NewPageShow || !node.HasChildren) return;

    if (ShowDialogFunc == null)
    {
        // 记录浏览历史
        History.Push(NavigationManager.Uri, RootId, node.Value?.Id);

        if (node?.Value == null) return;
        Snackbar.Add($"点击{node.Text}", Severity.Info);
        NavigationManager.NavigateTo($"{CurrentPage}/rootId/{node.Value.Id}", forceLoad: true, replace: true);
        
        return;
    }

    // 触发外部回调，由调用方决定导航行为
    await OnClickItemText.InvokeAsync(node);
}
```

### 1.6 调用点：TreePageSky.razor（返回 / 主页按钮）

```csharp
private void StartClick()
{
    History.Clear();
    NavigationManager.NavigateTo(SolutionService.StartPage);
}

private void BackClick()
{
    if (History.Peek() != null)
    {
        var entry = History.Pop();
        if (entry == null) return;

        var targetUrl = $"{CurrentPage}/";
        if (entry.RootId != null) targetUrl += $"rootId/{entry.RootId}/";
        if (entry.ClickNodeId != null) targetUrl += $"ClickNodeId/{entry.ClickNodeId}";

        NavigationManager.NavigateTo(targetUrl, forceLoad: true);
    }
    else
    {
        if (string.IsNullOrEmpty(BackPage))
        {
            OnBackClick.InvokeAsync();
        }
        else
        {
            NavigationManager.NavigateTo(BackPage);
        }
    }
}
```

> 附：`TreeSky.razor.Node.cs` 中另一段被注释的 `ClickBack/StartClick` 历史逻辑（当前未启用），完整文件见仓库。

---

## 2. DialogConfig + ToDialogOptions() 扩展 + TreeNodeDialogService

- 命名空间：`APromisedLand.Razor.Dialogs`（DialogConfig/DialogHelper）、`APromisedLand.Razor.DiberyTree.Services`（TreeNodeDialogService）
- 项目：`APromisedLand.Razor`

### 2.1 DialogConfig.cs（含 ToDialogOptions()）

```csharp
using MudBlazor;

namespace APromisedLand.Razor.Dialogs;

/// <summary>
/// 对话框配置选项
/// </summary>
public class DialogConfig
{
    /// <summary>对话框最大宽度</summary>
    public MaxWidth MaxWidth { get; set; } = MaxWidth.Small;

    /// <summary>是否显示关闭按钮</summary>
    public bool CloseButton { get; set; } = false;

    /// <summary>点击背景是否关闭</summary>
    public bool BackdropClick { get; set; } = false;

    /// <summary>是否全屏</summary>
    public bool FullScreen { get; set; } = false;

    /// <summary>是否全屏</summary>
    public bool FullWidth { get; set; } = true;
    
    /// <summary>位置</summary>
    public DialogPosition Position { get; set; } = DialogPosition.TopCenter;

    /// <summary>转换为 MudBlazor DialogOptions</summary>
    public DialogOptions ToDialogOptions() => new()
    {
        MaxWidth = MaxWidth,
        CloseButton = CloseButton,
        BackdropClick = BackdropClick,
        FullScreen = FullScreen,
        FullWidth = FullWidth,
        Position = Position
    };
}
```

### 2.2 DialogHelper.cs（ToDialogOptions 使用示例扩展）

```csharp
using APromisedLand.Razor.Dialogs.UnitsOfMeasure;
using APromisedLand.Razor.DiberyTree.Trees.Category;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DTOs.Units;
using MudBlazor;

namespace APromisedLand.Razor.Dialogs;

public static class DialogHelper
{
    public static async Task ShowCategoryTreeDialogAsync(this IDialogService dialogService, 
        ITreeItemData<CategoryTree>? node = null)
    {
        var parameters = new DialogParameters<CategoryTreeDialogPage>
        {
            { x => x.ClickNode, node },
        };

        var options = new DialogConfig
        {
            FullScreen = true,
            FullWidth = true,
            MaxWidth = MaxWidth.False,
        }.ToDialogOptions();

        var dialog = await dialogService.ShowAsync<CategoryTreeDialogPage>("分类",
            parameters, options);

        var result = await dialog.Result;

        //return result.Canceled == false;
    }
    
    public static async Task<UnitOfMeasureDto?> ShowUnitOfMeasureSelectDialogAsync(this IDialogService dialogService)
    {
        var parameters = new DialogParameters<UnitOfMeasureSelectDialog>
        {
            //{ x => x.TestingArgs, testingArgs }
        };

        var options = new DialogConfig
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Small,
        }.ToDialogOptions();

        var dialog = await dialogService.ShowAsync<UnitOfMeasureSelectDialog>("计量单位",
            parameters, options);

        var result = await dialog.Result;

        if (result is { Canceled: false, Data: UnitOfMeasureDto selected })
        {
            return selected;
        }
        
        return null;
    }
    
    public static async Task ShowUnitOfMeasureDialogAsync(this IDialogService dialogService)
    {
        var parameters = new DialogParameters<UnitsOfMeasureDialogPage>
        {
            //{ x => x.TestingArgs, testingArgs }
        };

        var options = new DialogConfig
        {
            FullScreen = true,
            FullWidth = true,
            MaxWidth = MaxWidth.False,
        }.ToDialogOptions();

        var dialog = await dialogService.ShowAsync<UnitsOfMeasureDialogPage>("计量单位",
            parameters, options);

        var result = await dialog.Result;

        //return result.Canceled == false;
    }
}
```

### 2.3 TreeNodeDialogService.cs（全文件）

```csharp
using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.DiberyTree.Nodes;
using APromisedLand.Razor.DiberyTree.Models;
using APromisedLand.Razor.DiberyTree.Pages;
using APromisedLand.Shared.DiberyTree.Interfaces;
using MudBlazor;
using APromisedLand.Razor.DiberyTree.Attributes;
using APromisedLand.Razor.DiberyTree.Base;
using APromisedLand.Razor.Services;
using MudBlazor.Extensions;

namespace APromisedLand.Razor.DiberyTree.Services;

/// <summary>
/// 树节点对话框服务
/// </summary>
/// <typeparam name="TItem">节点类型，必须实现 ITreeNode</typeparam>
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
            { x => x.EditFunc, blazorService.ShowUnitTreeDialogPageAsync}
        };
        
        // var options = new DialogOptionsEx
        // {
        //     MaximizeButton = true, // 启用最大化/还原按钮
        //     CloseButton = false,    // 同时显示关闭按钮
        //     FullWidth = true,
        //     MaxWidth = MaxWidth.Small,
        //     BackdropClick = false,
        //     Position = DialogPosition.TopCenter,
        //     Resizeable = true,
        //     DragMode = MudDialogDragMode.Simple
        // };

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

    #region 删除确认对话框

    // public async Task<bool> ShowDeleteDialogAsync(
    //     TItem node,
    //     bool hasChildren = false,
    //     DialogConfig? config = null)
    // {
    //     var parameters = new DialogParameters
    //     {
    //         { "Node", node },
    //         { "HasChildren", hasChildren }
    //     };
    //
    //     var options = (config ?? new DialogConfig
    //     {
    //         MaxWidth = MaxWidth.ExtraSmall,
    //         CloseButton = false,
    //         BackdropClick = false
    //     }).ToDialogOptions();
    //
    //     var dialog = await dialogService.ShowAsync<TreeNodeDelebbteDialog<TItem>>("确认删除", parameters, options);
    //
    //     var result = await dialog.Result;
    //     return result is { Canceled: false };
    // }

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

    public async Task<ParentSelectResult<TItem>?> ShowParentSelectDialogAsync(TreeSky<TItem> treeSky,
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

    public async Task<SortResult<TItem>?> ShowSortDialogAsync(TreeSky<TItem> treeSky,
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
        var parameters = new DialogParameters<NodeAttributesDialog<TItem>>
        {
            { x => x.NodeId, nodeId }
        };

        var options = (config ?? new DialogConfig
        {
            FullWidth = true,
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<NodeAttributesDialog<TItem>>(
            "节点属性", parameters, options);

        var result = await dialog.Result;
        // 只要不是取消就返回 true（表示可能已修改）
        return result is not { Canceled: true };
    }
    
    

    

    #endregion
}
```

---

## 3. ApiResponse<T> + DiberyTreeApiClient

### 3.1 ApiResponse.cs

- 命名空间：`APromisedLand.Shared.DTOs`
- 项目：`APromisedLand.Shared`
- 文件：`DTOs/ApiResponse.cs`

```csharp
namespace APromisedLand.Shared.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, T? data = default) =>
        new() { Success = false, Message = message, Data = data };
}

/// <summary>
/// 统一 API 响应辅助（非泛型，用于无数据返回）
/// </summary>
public static class ApiResponse
{
    public static ApiResponse<object> Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiResponse<object> Fail(string message) =>
        new() { Success = false, Message = message };
}
```

### 3.2 DiberyTreeApiClient.cs（全文件）

- 命名空间：`APromisedLand.MauiBlazor.DiberyTree.Services`
- 项目：`APromisedLand.MauiBlazor`
- 文件：`DiberyTree/Services/DiberyTreeApiClient.cs`

```csharp
// DiberyTreeApiClient.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DTOs;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

/// <summary>
/// 泛型树 API 客户端，用于调用后端的 TreeControllerBase<T>，
/// 包含树节点 CRUD 以及节点属性值的操作（属性定义已分离至 AttributeApiClient，表数据见 TableValueApiClient）。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class DiberyTreeApiClient<T>(HttpClient httpClient)
{
    private readonly string _basePath = typeof(T).Name; // 例如 "CategoryTree"

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 发送请求并从标准 ApiResponse 中提取 Data，若失败则抛出异常。
    /// </summary>
    private async Task<TData> SendAndGetDataAsync<TData>(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        
        await EnsureSuccessWithApiResponseAsync(response);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
        if (apiResponse?.Success != true)
            throw new Exception(apiResponse?.Message ?? "操作失败，未返回具体错误信息");
        
        return apiResponse.Data!;
    }

    /// <summary>
    /// 针对可能返回 404 的方法，返回 null 而不抛出异常。
    /// </summary>
    private async Task<TData?> SendAndGetDataOrNullAsync<TData>(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        where TData : class
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
        return apiResponse?.Success == true ? apiResponse.Data : null;
    }

    /// <summary>
    /// 检查状态码，若失败则尝试从响应中读取 ApiResponse 消息并抛出。
    /// </summary>
    private static async Task EnsureSuccessWithApiResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        // 尝试读取标准 ApiResponse 中的错误信息
        // try
        // {
            var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
                throw new HttpRequestException($"请求失败: {errorResponse.Message}", null, response.StatusCode);
        // }
        // catch (Exception ex)
        // {
        //     // 若无法解析，则回退到默认 EnsureSuccessStatusCode
        //     // response.EnsureSuccessStatusCode();
        // }
    }

    // ==================== 树节点操作 ====================

    /// <summary>
    /// 获取所有根节点；若指定 rootId，则获取该特定根节点（对应 roots/{rootId} 路由）。
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> GetRootNodesAsync(
        string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrEmpty(rootId)
            ? $"{_basePath}/roots"
            : $"{_basePath}/roots/{Uri.EscapeDataString(rootId)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAndGetDataAsync<IReadOnlyList<TreeNodeDto<T>>>(request, cancellationToken);
    }

    /// <summary>
    /// 获取指定父节点的子节点（懒加载）
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> GetChildrenAsync(
        string parentId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_basePath}/children/{Uri.EscapeDataString(parentId)}");
        return await SendAndGetDataAsync<IReadOnlyList<TreeNodeDto<T>>>(request, cancellationToken);
    }

    /// <summary>
    /// 获取从根节点到指定节点的祖先路径
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAncestorPathAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_basePath}/{Uri.EscapeDataString(nodeId)}/ancestors");
        return await SendAndGetDataAsync<IReadOnlyList<string>>(request, cancellationToken);
    }

    /// <summary>
    /// 条件查询节点（分页、搜索、过滤）
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> QueryNodesAsync(
        TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_basePath}/query")
        {
            Content = JsonContent.Create(queryParams)
        };
        return await SendAndGetDataAsync<IReadOnlyList<TreeNodeDto<T>>>(request, cancellationToken);
    }

    /// <summary>
    /// 获取完整树（包含所有后代）；若指定 rootId，则从该节点开始展开。
    /// </summary>
    public async Task<TreeNodeDto<T>?> GetFullTreeAsync(
        string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/full";
        if (!string.IsNullOrEmpty(rootId))
            url += $"?rootId={Uri.EscapeDataString(rootId)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAndGetDataOrNullAsync<TreeNodeDto<T>>(request, cancellationToken);
    }

    /// <summary>
    /// 创建新节点
    /// </summary>
    public async Task<TreeNodeDto<T>> CreateNodeAsync(
        TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = JsonContent.Create(node)
        };
        return await SendAndGetDataAsync<TreeNodeDto<T>>(request, cancellationToken);
    }

    /// <summary>
    /// 更新节点的子项顺序（Reorder）
    /// </summary>
    public async Task<TreeNodeDto<T>> UpdateChildrenAsync(
        TreeNodeDto<T> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_basePath}/children")
        {
            Content = JsonContent.Create(nodeDto)
        };
        return await SendAndGetDataAsync<TreeNodeDto<T>>(request, cancellationToken);
    }

    /// <summary>
    /// 更新节点信息（不包括 ParentId，请使用 Move 方法移动）
    /// </summary>
    public async Task<TreeNodeDto<T>> UpdateNodeAsync(
        string id,
        TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{_basePath}/{Uri.EscapeDataString(id)}")
        {
            Content = JsonContent.Create(node)
        };
        return await SendAndGetDataAsync<TreeNodeDto<T>>(request, cancellationToken);
    }

    /// <summary>
    /// 删除节点及其所有子节点（不存在返回 false，其他失败抛异常）
    /// </summary>
    public async Task<bool> DeleteNodeAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{_basePath}/{Uri.EscapeDataString(nodeId)}");
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
        return apiResponse?.Success == true && apiResponse.Data;
    }

    /// <summary>
    /// 移动节点到新的父节点（null 表示移至根）。若节点或新父节点不存在返回 false，其他失败抛异常。
    /// </summary>
    public async Task<bool> MoveNodeAsync(
        string nodeId,
        string? newParentId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/move?nodeId={Uri.EscapeDataString(nodeId)}";
        if (!string.IsNullOrEmpty(newParentId))
            url += $"&newParentId={Uri.EscapeDataString(newParentId)}";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
        return apiResponse?.Success == true && apiResponse.Data;
    }

    // 属性定义操作已分离至 AttributeApiClient（属性定义为全局资源，不耦合具体树）
}
```

> 注：原文件中属性值操作（AddValue/GetSingleValue/GetAllValues/UpdateValue/DeleteValue）整段为注释代码，已从汇编中省略，完整内容见仓库源文件。

---

## 4. TreeQueryParams（QueryNodesAsync 入参）

- 命名空间：`APromisedLand.Shared.DiberyTree.Models`
- 项目：`APromisedLand.Shared`
- 文件：`DiberyTree/Models/TreeQueryParams.cs`
- 消费方：`DiberyTreeApiClient<T>.QueryNodesAsync`（见 3.2，`POST {basePath}/query`）

```csharp
namespace APromisedLand.Shared.DiberyTree.Models;

/// <summary>
/// 树节点查询参数
/// </summary>
public class TreeQueryParams
{
    /// <summary>
    /// 父节点ID（根节点传 null 或空字符串）
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// 搜索关键词（可选）
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// 是否只加载有子节点的节点
    /// </summary>
    public bool OnlyWithChildren { get; set; }

    /// <summary>
    /// 最大深度（0表示不限制）
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// 分页参数 - 页码
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 分页参数 - 每页大小
    /// </summary>
    public int PageSize { get; set; } = 100;
}
```

---

## 5. NodeActionResult<TItem>

- 命名空间：`APromisedLand.Razor.DiberyTree.Models`
- 项目：`APromisedLand.Razor`
- 文件：`DiberyTree/Models/NodeActionResult.cs`
- 消费方：`TreeNodeDialogService` 的 ShowActionsDialogAsync / ShowTreeFileDialogAsync / ShowTreeImage/Video/LocationDialogAsync

```csharp
using APromisedLand.Razor.DiberyTree.Enums;
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Razor.DiberyTree.Models;

/// <summary>
/// 节点操作结果
/// </summary>
/// <typeparam name="TItem">节点类型</typeparam>
public class NodeActionResult<TItem> where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>操作类型</summary>
    public NodeAction Action { get; set; }

    /// <summary>目标节点</summary>
    public required TItem Node { get; set; }
}
```

---

## 6. ParentSelectResult<TItem>

- 命名空间：`APromisedLand.Razor.DiberyTree.Models`
- 项目：`APromisedLand.Razor`
- 文件：`DiberyTree/Models/ParentSelectResult.cs`
- 消费方：`TreeNodeDialogService.ShowParentSelectDialogAsync`

```csharp
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Razor.DiberyTree.Models;

/// <summary>
/// 父节点选择结果
/// </summary>
/// <typeparam name="TItem">节点类型</typeparam>
public class ParentSelectResult<TItem> where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>是否确认选择</summary>
    public bool IsConfirmed { get; set; }

    /// <summary>选中的父节点（null 表示根节点）</summary>
    public TItem? SelectedParent { get; set; }

    /// <summary>选中节点的路径ID列表</summary>
    public List<string> SelectedPath { get; set; } = new();
}
```

---

## 7. SortResult<TItem>

- 命名空间：`APromisedLand.Razor.DiberyTree.Models`
- 项目：`APromisedLand.Razor`
- 文件：`DiberyTree/Models/SortResult.cs`
- 消费方：`TreeNodeDialogService.ShowSortDialogAsync`

```csharp
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Razor.DiberyTree.Models;

/// <summary>
/// 节点排序结果
/// </summary>
/// <typeparam name="TItem">节点类型</typeparam>
public class SortResult<TItem> where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>是否确认保存</summary>
    public bool IsConfirmed { get; set; }

    /// <summary>排序后的节点列表（扁平化，包含新的 SortOrder 和 ParentId）</summary>
    public List<SortItem<TItem>> SortedItems { get; set; } = new();

    /// <summary>是否有层级变更（移动到其他父节点下）</summary>
    public bool HasHierarchyChanges => SortedItems.Any(i => i.IsParentChanged);

    /// <summary>是否有顺序变更</summary>
    public bool HasOrderChanges => SortedItems.Any(i => i.IsOrderChanged);
}

/// <summary>
/// 排序项详情
/// </summary>
public class SortItem<TItem> where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>节点</summary>
    public required TItem Node { get; set; }

    /// <summary>新的排序序号</summary>
    public int NewSortOrder { get; set; }

    /// <summary>新的父节点ID（null 表示根节点）</summary>
    public string? NewParentId { get; set; }

    /// <summary>原始排序序号</summary>
    public int OriginalSortOrder { get; set; }

    /// <summary>原始父节点ID</summary>
    public string? OriginalParentId { get; set; }

    /// <summary>是否父节点变更</summary>
    public bool IsParentChanged => NewParentId != OriginalParentId;

    /// <summary>是否顺序变更</summary>
    public bool IsOrderChanged => NewSortOrder != OriginalSortOrder;
}
```

---

## 8. NodeOperationOutcome<TItem>

- 命名空间：`APromisedLand.Razor.DiberyTree.Models`
- 项目：`APromisedLand.Razor`
- 文件：`DiberyTree/Models/NodeOperationOutcome.cs`
- 消费方：`TreeNodeDialogService.ExecuteNodeOperationAsync`

```csharp
using APromisedLand.Razor.DiberyTree.Enums;
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Razor.DiberyTree.Models;

public class NodeOperationOutcome<TItem> where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>用户选择的操作</summary>
    public required NodeAction Action { get; set; }

    /// <summary>目标节点</summary>
    public required TItem Node { get; set; }
}
```

---

## 9. MessageDialog（Razor 组件）+ MessageService

### 9.1 MessageDialog.razor（全文件）

- 命名空间：`APromisedLand.Razor.Components`
- 项目：`APromisedLand.Razor`
- 文件：`Components/MessageDialog.razor`
- 注意：`APromisedLand.SharedRazor/Components/MessageDialog.razor` 另有一份同名组件，本汇编收录 Razor 项目的版本。

```razor
@using Microsoft.JSInterop

@inject ISnackbar Snackbar
@* MessageDialog.razor *@
<DialogSky Title="@Title"
           TitleClass="mud-theme-primary"
           SubmitButtonVisible="@SubmitButtonVisible"
           IconsVisible="@IconsVisible"
           SubmitButtonText="@SubmitButtonText"
           CancelButtonText="@CancelButtonText"
           OnSubmitClick="OnSubmit">
    <ToolBarContent>
        <MudSpacer />
        <MudIconButton Icon="@Icons.Material.Filled.ContentCopy"
                       Size="Size.Small"
                       Color="Color.Default"
                       OnClick="@CopyToClipboard"/>
    </ToolBarContent>
    <DialogContent>
        <MudContainer class="my-4">
            <MudText Typo="Typo.body1"
                     Color="Color.Warning"
                     Style="word-wrap: break-word; overflow-wrap: break-word;">@Message</MudText>
        </MudContainer>
    </DialogContent>
</DialogSky>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public bool SubmitButtonVisible { get; set; }
    [Parameter] public bool IconsVisible { get; set; }
    [Parameter] public string SubmitButtonText { get; set; } = "提交";
    [Parameter] public string CancelButtonText { get; set; } = "关闭";

    [Parameter] public required string Title { get; set; }
    [Parameter] public required string Message { get; set; }


    [Inject] private IJSRuntime JS { get; set; } = default!;

    private void OnSubmit()
    {
        MudDialog?.Close(true);
    }

    private async Task CopyToClipboard()
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", Message);
        
        Snackbar.Add("复制成功", Severity.Success, options =>
        {
            options.HideTransitionDuration = 3000;
            options.ShowCloseIcon = false;
        });
    }

}
```

### 9.2 MessageService.cs（全文件）

- 命名空间：`APromisedLand.Razor.Services`
- 项目：`APromisedLand.Razor`
- 文件：`Services/MessageService.cs`

```csharp
using APromisedLand.Razor.Components;
using APromisedLand.Shared.Helper;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Options;

namespace APromisedLand.Razor.Services;

public class MessageService(IDialogService dialogService, ISnackbar snackbar)
{
    public DialogOptionsEx DialogOptions { get; set; } = new DialogOptionsEx
    {
        MaximizeButton = true, // 启用最大化/还原按钮
        CloseButton = false, // 同时显示关闭按钮
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
        BackdropClick = false,
        Position = DialogPosition.Center,
        Resizeable = true,
        DragMode = MudDialogDragMode.Simple,
        AnimateClose = true,
    };

    public async Task<bool> DeleteBox(string? message, string? title = "删除")
    {
        var options = new MessageBoxOptions
        {
            Title = title,
            Message = message,
            YesText = "是",
            CancelText = "否",
        };

        var dialogOptions = new DialogOptions
        {
            BackdropClick = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
        };

        bool? result = await dialogService.ShowMessageBoxAsync(options, dialogOptions);

        return result ?? false;
    }
    
    public async Task<bool> DeleteBoxAsync(string message, string title = "警告 : 删除操作无法撤消！")
    {
        var options = new DialogParameters<MessageDialog>()
        {
            { x => x.Title, title },
            { x => x.Message, message },
            { x => x.SubmitButtonText, "确认" },
            { x => x.CancelButtonText, "取消" },
            { x => x.IconsVisible, false },
            { x => x.SubmitButtonVisible, true },
        };

        var dialogOptions = new DialogOptions
        {
            BackdropClick = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            CloseButton = false,
        };

// 获取对话框引用
        var dialogRef = await dialogService.ShowExAsync<MessageDialog>("提示", options, dialogOptions);
        // if (dialogRef == null)
        //     return false; // 或按需处理异常

        // 等待用户操作结果
        var dialogResult = await dialogRef.Result;

        // 如果用户取消了对话框或结果为空，返回 false
        if (dialogResult == null || dialogResult.Canceled)
            return false;

        // 用户点击了确认按钮，返回 true
        return true;
    }

    public async Task<bool> BoolBoxAsync(string message = "删除操作无法撤消！", string title = "请确认")
    {
        var result = await dialogService.ShowMessageBoxAsync(
            title, message,
            yesText: "确认！", cancelText: "取消",
            options: new DialogOptions
            {
                MaxWidth = MaxWidth.ExtraSmall,
                BackdropClick = false,
                FullWidth = true
            });

        return result != null;
    }

    //snackbar 通知
    public void Details(
        string message,
        string detail)
    {
        snackbar.Add(message, Severity.Error, config =>
        {
            config.VisibleStateDuration = 3000;
            config.ShowCloseIcon = false;
            config.Action = "查看";
            config.ActionColor = Color.Info;
            config.ActionVariant = Variant.Filled;
            config.OnClick = async e => { await HelpAsync(dialogService, message, detail); };
        });
    }

    private static async Task HelpAsync(IDialogService dialogService,
        string message, string details)
    {
        //snackbar.Add(message);
        var options = new DialogParameters<MessageDialog>()
        {
            { x => x.Title, message },
            { x => x.Message, details },
            { x => x.CancelButtonText, "确定" },
        };

        var dialogOptions = new DialogOptions
        {
            BackdropClick = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            CloseButton = false,
        };

        await dialogService.ShowExAsync<MessageDialog>("提示", options, dialogOptions);
    }

    public void Success(string? message)
    {
        snackbar.Add(message ?? "没有信息。", Severity.Success,
            config =>
            {
                config.VisibleStateDuration = 3000;
                config.ShowCloseIcon = true;
                config.SnackbarVariant = Variant.Outlined;
            });
    }

    public void Warning(string? message)
    {
        snackbar.Add(message ?? "没有信息。", Severity.Warning,
            config =>
            {
                config.VisibleStateDuration = 3000;
                config.ShowCloseIcon = true;
                config.SnackbarVariant = Variant.Outlined;
            });
    }

    public void Info(string message) //=> snackbar.Add(message, Severity.Success);
    {
        snackbar.Add(message, Severity.Info, config => { config.VisibleStateDuration = 3000; });
    }
    
        public void Error(string? message)
    {
        var duration = 5000;
#if DEBUG
        duration = 10000;
        message = $"错误:{message?.Ellipsis(30)}";
#endif
        snackbar.Add(message ?? "没有信息。", Severity.Error,
            config =>
            {
                config.VisibleStateDuration = duration;
                config.SnackbarVariant = Variant.Filled;
            });
    }
}
```

---

## 10. MudBlazor.Extensions：ShowExAsync / DialogOptionsEx 用法

`ShowExAsync` 与 `DialogOptionsEx` 来自第三方 NuGet 包 **MudBlazor.Extensions**（`using MudBlazor.Extensions;` / `using MudBlazor.Extensions.Options;`），非项目自有代码。项目内的集中使用点：

### 10.1 BlazorService.cs（DialogOptionsEx 全局模板）

- 命名空间：`APromisedLand.Razor.Services`
- 项目：`APromisedLand.Razor`
- 文件：`Services/BlazorService.cs`

```csharp
using APromisedLand.Razor.Helper.Blazor;
using APromisedLand.Shared.Models;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Options;

namespace APromisedLand.Razor.Services;

public partial class BlazorService(IDialogService dialogService, ISnackbar snackbar)
{
     public DialogOptionsEx DialogOptions { get; set; } =  new DialogOptionsEx
     {
          MaximizeButton = true, // 启用最大化/还原按钮
          CloseButton = false,    // 同时显示关闭按钮
          FullWidth = true,
          MaxWidth = MaxWidth.Small,
          BackdropClick = false,
          Position = DialogPosition.TopCenter,
          Resizeable = true,
          DragMode = MudDialogDragMode.Simple,
          AnimateClose = true,
     };
}
```

### 10.2 使用点一览

| 使用文件 | 用法 |
|---|---|
| `BlazorService.cs` | 定义全局 `DialogOptionsEx` 模板（MaximizeButton / Resizeable / DragMode / AnimateClose） |
| `MessageService.cs` | 属性 `DialogOptionsEx DialogOptions`；`ShowExAsync<MessageDialog>(...)`（DeleteBoxAsync / HelpAsync） |
| `BlazorService.UnitTree.cs` | `new DialogOptionsEx { ... }` + `ShowExAsync<UnitTreeDialogPage>(...)` |
| `BlazorService.CategoryTree.cs` | `new DialogOptionsEx { FullScreen = true, ... }` + `ShowExAsync<CategoryTreeDialogPage>(...)` |
| `TreeNodeDialogService.cs` | `blazorService.DialogOptions` + `ShowExAsync<TreeDialogPageSky<>>` / `ShowExAsync<TreeSelectDialogSky<>>` |

### 10.3 典型调用形态

```csharp
var dialog = await dialogService.ShowExAsync<TComponent>("标题", parameters, options);
var result = await dialog.Result;
if (result?.Canceled != false || result.Data is not TData data)
    return null;
return data;
```

> `DialogOptionsEx` 相对原生 `DialogOptions` 的扩展能力：`MaximizeButton`、`Resizeable`、`DragMode = MudDialogDragMode.Simple`、`AnimateClose` 等。
> 项目内同时存在两套选项体系：简单对话框用 `DialogConfig.ToDialogOptions()`（第 2 节），需要拖拽/最大化的对话框用 `DialogOptionsEx` + `ShowExAsync`。

---

## 11. UnitTree 模型 + BlazorService.UnitTree

### 11.1 UnitTree.cs（全文件，含 SeedData）

- 命名空间：`APromisedLand.Shared.DiberyTree.Models`
- 项目：`APromisedLand.Shared`
- 文件：`DiberyTree/Models/UnitTree.cs`

```csharp
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Shared.DiberyTree.Models;

public class UnitTree : ITreeNodeBase<UnitTree>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? Description { get; set; }
    public bool CanHaveChildren { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }
    public UnitTree? Parent { get; set; }

    public string Text()
    {
        var text = string.IsNullOrEmpty(Abbreviation) ? Name : $"{Name} 【{Abbreviation}】";
        return text;
    }

    public static List<UnitTree> SeedData()
    {
        var nodes = new List<UnitTree>();
        int sortOrder = 0;

        // ========== 分类节点（固定 GUID） ==========
        const string CURRENCY_ID = "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f";
        const string LENGTH_ID = "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d";
        const string MASS_ID = "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e";
        const string TIME_ID = "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f";
        const string TEMP_ID = "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a";
        const string CURRENT_ID = "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b";
        const string VOLTAGE_ID = "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c";
        const string POWER_ID = "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d";
        const string AREA_ID = "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e";
        const string VOLUME_ID = "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f";
        const string SPEED_ID = "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a";
        const string PRESSURE_ID = "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b";
        const string ENERGY_ID = "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c";
        const string FREQUENCY_ID = "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d";
        const string ANGLE_ID = "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e";

        // 根节点（允许有子项，且有子项）
        nodes.Add(new UnitTree
        {
            Id = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            Name = "计量单位",
            Abbreviation = "",
            Description = "",
            ParentId = null,
            CanHaveChildren = true,
            SortOrder = 0,
            HasChildren = true
        });

        // 分类节点（允许有子项，且有子项）
        nodes.Add(new UnitTree
        {
            Id = CURRENCY_ID,
            Name = "货币",
            Abbreviation = "",
            Description = "货币计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = LENGTH_ID,
            Name = "长度",
            Abbreviation = "",
            Description = "长度计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = MASS_ID,
            Name = "质量",
            Abbreviation = "",
            Description = "质量计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = TIME_ID,
            Name = "时间",
            Abbreviation = "",
            Description = "时间计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = TEMP_ID,
            Name = "温度",
            Abbreviation = "",
            Description = "温度计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = CURRENT_ID,
            Name = "电流",
            Abbreviation = "",
            Description = "电流计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = VOLTAGE_ID,
            Name = "电压",
            Abbreviation = "",
            Description = "电压计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = POWER_ID,
            Name = "功率",
            Abbreviation = "",
            Description = "功率计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = AREA_ID,
            Name = "面积",
            Abbreviation = "",
            Description = "面积计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = VOLUME_ID,
            Name = "体积",
            Abbreviation = "",
            Description = "体积计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = SPEED_ID,
            Name = "速度",
            Abbreviation = "",
            Description = "速度计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = PRESSURE_ID,
            Name = "压力",
            Abbreviation = "",
            Description = "压力计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = ENERGY_ID,
            Name = "能量",
            Abbreviation = "",
            Description = "能量计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = FREQUENCY_ID,
            Name = "频率",
            Abbreviation = "",
            Description = "频率计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = ANGLE_ID,
            Name = "角度",
            Abbreviation = "",
            Description = "角度计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });

        // ========== 单位节点（不允许有子项，且当前无子项） ==========
        // 货币单位
        nodes.Add(new UnitTree
        {
            Id = "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c", // 人民币
            Name = "元",
            Abbreviation = "CNY",
            Description = "人民币",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e6b7c8d9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", // 美元
            Name = "美元",
            Abbreviation = "USD",
            Description = "美元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d5c6d7e8-f9a0-4b1c-2d3e-4f5a6b7c8d9e", // 欧元
            Name = "欧元",
            Abbreviation = "EUR",
            Description = "欧元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f", // 英镑
            Name = "英镑",
            Abbreviation = "GBP",
            Description = "英镑",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e", // 日元
            Name = "日元",
            Abbreviation = "JPY",
            Description = "日元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
// 新增港元和澳元
        nodes.Add(new UnitTree
        {
            Id = "a5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", // 港元
            Name = "港元",
            Abbreviation = "HKD",
            Description = "港元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", // 澳元
            Name = "澳元",
            Abbreviation = "AUD",
            Description = "澳元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        // 长度单位
        nodes.Add(new UnitTree
        {
            Id = "c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c",
            Name = "米",
            Abbreviation = "m",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
            Name = "千米",
            Abbreviation = "km",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
            Name = "厘米",
            Abbreviation = "cm",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "f3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f",
            Name = "毫米",
            Abbreviation = "mm",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a",
            Name = "英里",
            Abbreviation = "mi",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b",
            Name = "码",
            Abbreviation = "yd",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "c6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c",
            Name = "英尺",
            Abbreviation = "ft",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d",
            Name = "英寸",
            Abbreviation = "in",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 质量单位
        nodes.Add(new UnitTree
        {
            Id = "e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e",
            Name = "千克",
            Abbreviation = "kg",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f",
            Name = "克",
            Abbreviation = "g",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a",
            Name = "毫克",
            Abbreviation = "mg",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b",
            Name = "吨",
            Abbreviation = "t",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "c2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c",
            Name = "磅",
            Abbreviation = "lb",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d",
            Name = "盎司",
            Abbreviation = "oz",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 时间单位
        nodes.Add(new UnitTree
        {
            Id = "e4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e",
            Name = "秒",
            Abbreviation = "s",
            Description = "",
            ParentId = TIME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "f5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f",
            Name = "分钟",
            Abbreviation = "min",
            Description = "",
            ParentId = TIME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a",
            Name = "小时",
            Abbreviation = "h",
            Description = "",
            ParentId = TIME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b",
            Name = "天",
            Abbreviation = "d",
            Description = "",
            ParentId = TIME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 温度单位
        nodes.Add(new UnitTree
        {
            Id = "c8a9b0c1-d2e3-4f4a-5b6c-7d8e9f0a1b2c",
            Name = "摄氏度",
            Abbreviation = "°C",
            Description = "",
            ParentId = TEMP_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d9b0c1d2-e3f4-4a5b-6c7d-8e9f0a1b2c3d",
            Name = "华氏度",
            Abbreviation = "°F",
            Description = "",
            ParentId = TEMP_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e0c1d2e3-f4a5-4b6c-7d8e-9f0a1b2c3d4e",
            Name = "开尔文",
            Abbreviation = "K",
            Description = "",
            ParentId = TEMP_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 电流单位
        nodes.Add(new UnitTree
        {
            Id = "f1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f",
            Name = "安培",
            Abbreviation = "A",
            Description = "",
            ParentId = CURRENT_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a2e3f4a5-b6c7-4d8e-9f0a-1b2c3d4e5f6a",
            Name = "毫安",
            Abbreviation = "mA",
            Description = "",
            ParentId = CURRENT_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b3f4a5b6-c7d8-4e9f-0a1b-2c3d4e5f6a7b",
            Name = "微安",
            Abbreviation = "µA",
            Description = "",
            ParentId = CURRENT_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 电压单位
        nodes.Add(new UnitTree
        {
            Id = "c4a5b6c7-d8e9-4f0a-1b2c-3d4e5f6a7b8c",
            Name = "伏特",
            Abbreviation = "V",
            Description = "",
            ParentId = VOLTAGE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d",
            Name = "千伏",
            Abbreviation = "kV",
            Description = "",
            ParentId = VOLTAGE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e",
            Name = "毫伏",
            Abbreviation = "mV",
            Description = "",
            ParentId = VOLTAGE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 功率单位
        nodes.Add(new UnitTree
        {
            Id = "f7d8e9f0-a1b2-4c3d-4e5f-6a7b8c9d0e1f",
            Name = "瓦特",
            Abbreviation = "W",
            Description = "",
            ParentId = POWER_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a8e9f0a1-b2c3-4d4e-5f6a-7b8c9d0e1f2a",
            Name = "千瓦",
            Abbreviation = "kW",
            Description = "",
            ParentId = POWER_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b9f0a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b",
            Name = "兆瓦",
            Abbreviation = "MW",
            Description = "",
            ParentId = POWER_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a00be966-be2e-484e-92b6-9706494ac775",
            Name = "马力",
            Abbreviation = "hp",
            Description = "",
            ParentId = POWER_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 面积单位
        nodes.Add(new UnitTree
        {
            Id = "e88b04db-40ac-4bb7-b420-1f3b37180673",
            Name = "平方米",
            Abbreviation = "m²",
            Description = "",
            ParentId = AREA_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "0d7ebe17-93ae-4e4c-92f4-063a124cd181",
            Name = "平方公里",
            Abbreviation = "km²",
            Description = "",
            ParentId = AREA_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a4c312d3-023e-4d4e-b5a7-fb7fcbd55c56",
            Name = "公顷",
            Abbreviation = "ha",
            Description = "",
            ParentId = AREA_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "fefa26a5-d608-411c-b637-469a886e558c",
            Name = "亩",
            Abbreviation = "亩",
            Description = "",
            ParentId = AREA_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 体积单位
        nodes.Add(new UnitTree
        {
            Id = "2d21c35a-4251-479e-b814-060b2fc84445",
            Name = "立方米",
            Abbreviation = "m³",
            Description = "",
            ParentId = VOLUME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "7f0af6a9-ba1a-469c-b967-e32afe43cad2",
            Name = "升",
            Abbreviation = "L",
            Description = "",
            ParentId = VOLUME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "780e7a01-350d-45ec-b963-b36a996de614",
            Name = "毫升",
            Abbreviation = "mL",
            Description = "",
            ParentId = VOLUME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 速度单位
        nodes.Add(new UnitTree
        {
            Id = "4cbec89d-3f52-4db3-9ab0-faeeb841ffbf",
            Name = "米/秒",
            Abbreviation = "m/s",
            Description = "",
            ParentId = SPEED_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "64e918fb-ee9d-45c7-b35a-2a55f5a5fe62",
            Name = "千米/小时",
            Abbreviation = "km/h",
            Description = "",
            ParentId = SPEED_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "405ae7a3-8a13-479d-bc1a-6f9d3c15e521",
            Name = "英里/小时",
            Abbreviation = "mph",
            Description = "",
            ParentId = SPEED_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 压力单位
        nodes.Add(new UnitTree
        {
            Id = "221d3c45-911f-4e94-9c3e-c13e6f2bcc76",
            Name = "帕斯卡",
            Abbreviation = "Pa",
            Description = "",
            ParentId = PRESSURE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "883a9940-84ec-4daa-8448-609461b984ea",
            Name = "千帕",
            Abbreviation = "kPa",
            Description = "",
            ParentId = PRESSURE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d5306eb8-324f-4088-a867-6fbc7141fd59",
            Name = "兆帕",
            Abbreviation = "MPa",
            Description = "",
            ParentId = PRESSURE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "8981bd9a-bd99-4f4c-b8af-038975b799be",
            Name = "巴",
            Abbreviation = "bar",
            Description = "",
            ParentId = PRESSURE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 能量单位
        nodes.Add(new UnitTree
        {
            Id = "5db494d7-2a9c-4ae0-86e0-d4bb0dfc7b81",
            Name = "焦耳",
            Abbreviation = "J",
            Description = "",
            ParentId = ENERGY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "279a6b18-6d01-4437-b95b-0480ca7adc98",
            Name = "千焦",
            Abbreviation = "kJ",
            Description = "",
            ParentId = ENERGY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e3c1f025-3b2b-461a-a5a1-015cb4e3fe38",
            Name = "千瓦时",
            Abbreviation = "kWh",
            Description = "",
            ParentId = ENERGY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 频率单位
        nodes.Add(new UnitTree
        {
            Id = "5e880060-9410-40d7-bcb4-545ccd0c1bb6",
            Name = "赫兹",
            Abbreviation = "Hz",
            Description = "",
            ParentId = FREQUENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "1a80ed3b-1b36-4d8b-b80b-3070dbc7979d",
            Name = "千赫",
            Abbreviation = "kHz",
            Description = "",
            ParentId = FREQUENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "41572712-95dd-4caf-b316-e1b924bc57c3",
            Name = "兆赫",
            Abbreviation = "MHz",
            Description = "",
            ParentId = FREQUENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 角度单位
        nodes.Add(new UnitTree
        {
            Id = "ed1b66d2-454b-453b-9d43-12605dffa456",
            Name = "度",
            Abbreviation = "°",
            Description = "",
            ParentId = ANGLE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "3d9088a3-7283-4f8f-b995-b193a57a6c2a",
            Name = "弧度",
            Abbreviation = "rad",
            Description = "",
            ParentId = ANGLE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        return nodes;
    }
}
```

### 11.2 BlazorService.UnitTree.cs（全文件）

- 命名空间：`APromisedLand.Razor.Services`
- 项目：`APromisedLand.Razor`
- 文件：`Services/BlazorService.UnitTree.cs`

```csharp
using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.DiberyTree.Base;
using APromisedLand.Razor.DiberyTree.Trees.Category;
using APromisedLand.Razor.DiberyTree.Trees.Unit;
using APromisedLand.Shared.DiberyTree.Models;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Components;
using MudBlazor.Extensions.Options;

namespace APromisedLand.Razor.Services;

public partial class BlazorService
{
    public async Task ShowUnitTreeDialogPageAsync( 
        ITreeItemData<UnitTree>? node = null)
    {
        var parameters = new DialogParameters<UnitTreeDialogPage>
        {
            { x => x.ClickNode, node },
        };
        
        var options = new DialogOptionsEx
        {
            MaximizeButton = true, // 启用最大化/还原按钮
            CloseButton = false,    // 同时显示关闭按钮
            FullWidth = true,
            MaxWidth = MaxWidth.Small,
            BackdropClick = false,
            Position = DialogPosition.TopCenter,
            Resizeable = true
        };

        var dialog = await dialogService.ShowExAsync<UnitTreeDialogPage>("单位注册",
            parameters, options);

        var result = await dialog.Result;
    }
}
```

---

## 12. UnitTreeClientService

- 命名空间：`APromisedLand.Razor.DiberyTree.Services`
- 项目：`APromisedLand.Razor`
- 文件：`DiberyTree/Services/UnitTreeClientService.cs`
- 实现 `ITreeClientService<UnitTree>`（接口来自 `APromisedLand.MauiBlazor.DiberyTree.Interfaces`），包装 `DiberyTreeApiClient<UnitTree>`；被 `UnitTreeDialogPage` 经 TreeSky/TreePageSky 间接使用。

```csharp
using APromisedLand.MauiBlazor.DiberyTree.Interfaces;
using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Razor.DiberyTree.Services;

public class UnitTreeClientService(
    DiberyTreeApiClient<UnitTree> treeClient) : ITreeClientService<UnitTree>
{
    public string Title { get; set; } = "计量单位";
    public bool NewPageShow { get; set; }
    public bool SelectLeaf { get; set; } = true;

    public async Task<IReadOnlyList<TreeNodeDto<UnitTree>>> LoadInitialDataAsync(string? rootId)
    {
        var items = await treeClient.GetRootNodesAsync(rootId);
        return OrderNodes(items);
    }

    public async Task<IReadOnlyList<TreeNodeDto<UnitTree>>> LoadChildrenAsync(UnitTree? parent)
    {
        var items = parent == null
            ? await treeClient.GetRootNodesAsync()
            : await treeClient.GetChildrenAsync(parent.Id);             
        return OrderNodes(items);
    }

    private static IReadOnlyList<TreeNodeDto<UnitTree>> OrderNodes(IEnumerable<TreeNodeDto<UnitTree>> items)
        => [.. items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text)];

    public async Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId)
    {
        var path = await treeClient.GetAncestorPathAsync(nodeId);
        return [.. path];
    }
}
```

---

## 13. CategoryTreeDialogPage + BlazorService.CategoryTree

### 13.1 CategoryTreeDialogPage.razor（全文件）

- 命名空间：`APromisedLand.Razor.DiberyTree.Trees.Category`
- 项目：`APromisedLand.Razor`
- 文件：`DiberyTree/Trees/Category/CategoryTreeDialogPage.razor`

```razor

@inject IDialogService DialogService
@inject ISnackbar Snackbar
@inject NavigationManager NavigationManager
@inject DiberyTreeApiClient<CategoryTree> TreeClient

@{
    RenderFragment<CategoryTree> actionTemplate = (context) =>
        @<MudStack Spacing="0">
            <MudTextField T="string"
                          Value="@context.Text()"
                          Label="名称"
                          Variant="Variant.Outlined"
                          ReadOnly="true"/>

            <MudTextField T="string"
                          Value="@context.Description"
                          Label="描述"
                          Variant="Variant.Outlined"
                          ReadOnly="true"
                          Lines="3"/>

            @* <MudChip T="string" *@
            @*          Color="@((context.CanHaveChildren ? Color.Success : Color.Error))" *@
            @*          Size="Size.Small"> *@
            @*     @(context.CanHaveChildren ? "已启用" : "已禁用") *@
            @* </MudChip> *@

            <BoolFieldSky Label="是否允许添加子节点"
                          @bind-Value="context.CanHaveChildren"/>
        </MudStack>;
}

@{
    RenderFragment<CategoryTree> editTemplate = (context) =>
        @<MudStack Spacing="0">
            <MudTextField T="string"
                          @bind-Value="context.Name"
                          Label="名称"
                          Variant="Variant.Outlined"
                          Required="true"
                          RequiredError="名称不能为空"
                          MaxLength="100"
                          Counter="100"
                          Immediate="true"/>

            <MudTextField T="string"
                          @bind-Value="context.Description"
                          Label="描述"
                          Variant="Variant.Outlined"
                          Lines="3"
                          MaxLength="500"
                          Counter="500"
                          Immediate="true"/>

            @* <MudSwitch T="bool" *@
            @*            @bind-Value="context.CanHaveChildren" *@
            @*            Label="启用状态" *@
            @*            Color="Color.Primary"/> *@

            <BoolFieldSky Label="是否允许添加子节点"
                          @bind-Value="context.CanHaveChildren"/>

        </MudStack>;
}

<TreeDialogPageSky TItem="CategoryTree"
                   Title="分类维护"
                   TitleToolBar="@(ClickNode?.Value?.Parent?.Text() ?? "分类树")"
                   RootId="@ClickNode?.Value?.Id"
                   ActionTemplate="@actionTemplate"
                   EditTemplate="@editTemplate"
                   ShowDialogFunc="@ShowDialogAsync"/>

@code {
    [Parameter] public ITreeItemData<CategoryTree>? ClickNode { get; set; }

    private async Task ShowDialogAsync(ITreeItemData<CategoryTree> node)
    {
        await DialogService.ShowCategoryTreeDialogAsync(node);
    }

}
```

### 13.2 BlazorService.CategoryTree.cs（全文件）

- 文件：`Services/BlazorService.CategoryTree.cs`

```csharp
using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.DiberyTree.Trees.Category;
using APromisedLand.Shared.DiberyTree.Models;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Options;

namespace APromisedLand.Razor.Services;

public partial class BlazorService
{
    public async Task ShowCategoryTreeDialogPageAsync(
        ITreeItemData<CategoryTree>? node = null)
    {
        var parameters = new DialogParameters<CategoryTreeDialogPage>
        {
            { x => x.ClickNode, node },
        };

        // var options = new DialogConfig
        // {
        //     FullScreen = true,
        //     FullWidth = true,
        //     MaxWidth = MaxWidth.False,
        // }.ToDialogOptions();

        var options = new DialogOptionsEx
        {
            MaximizeButton = false, // 启用最大化/还原按
            CloseButton = false, // 同时显示关闭按钮
            FullScreen = true,
            // FullWidth = true,
            MaxWidth = MaxWidth.False,
            BackdropClick = false,
            // Position = DialogPosition.TopCenter,
            // Resizeable = true
        };

        var dialog = await dialogService.ShowExAsync<CategoryTreeDialogPage>("分类",
            parameters, options);

        var result = await dialog.Result;

        //return result.Canceled == false;
    }
}
```

---

## 14. UnitTreeDialogPage

- 命名空间：`APromisedLand.Razor.DiberyTree.Trees.Unit`
- 项目：`APromisedLand.Razor`
- 文件：`DiberyTree/Trees/Unit/UnitTreeDialogPage.razor`
- 对话框弹出由 `BlazorService.ShowUnitTreeDialogPageAsync` 承担（见 11.2）。

```razor

@inject BlazorService BlazorService
@* UnitTreeDialogPage.razor *@
<TreeDialogPageSky TItem="UnitTree"
                   Title="单位注册"
                   TitleToolBar="@(ClickNode?.Value?.Parent?.Text() ?? "单位")"
                   RootId="@ClickNode?.Value?.Id"
                   ShowDialogFunc="@ShowDialogAsync" />

@code {
    [Parameter] public ITreeItemData<UnitTree>? ClickNode { get; set; }
    
    private async Task ShowDialogAsync(ITreeItemData<UnitTree> node)
    {
        await BlazorService.ShowUnitTreeDialogPageAsync(node);
    }
}
```

---

## 附：调用关系总览

```
TreePageSky.razor ──injects──► ITreeNavigationHistoryService (TreeNavigationHistoryService)
       │  Pop/Peek/Clear（返回按钮）
       ▼
TreeSky.razor ──injects──► History / TreeNodeDialogService<TItem> / MessageService / BlazorService
       │  History.Push（节点点击下钻）
       ▼
TreeNodeDialogService<TItem>
       ├─ DialogConfig.ToDialogOptions()  → ShowAsync（简单对话框）
       └─ BlazorService.DialogOptions (DialogOptionsEx) → ShowExAsync（可拖拽/最大化）
              ├─ TreeDialogPageSky / TreeSelectDialogSky
              ├─ CategoryTreeDialogPage（BlazorService.ShowCategoryTreeDialogPageAsync）
              └─ UnitTreeDialogPage（BlazorService.ShowUnitTreeDialogPageAsync）

数据层：
TreeSky ──► ITreeClientService<TItem>
                 └─ UnitTreeClientService ──► DiberyTreeApiClient<UnitTree>
                                                  └─ HTTP + ApiResponse<T> 解包
                                                  └─ QueryNodesAsync(TreeQueryParams)
```

对话框结果模型：

| 对话框 | 返回模型 |
|---|---|
| 节点操作 / 文件 / 图片 / 视频 / 位置 | `NodeActionResult<TItem>` |
| 父节点选择 | `ParentSelectResult<TItem>` |
| 拖拽排序 | `SortResult<TItem>`（含 `SortItem<TItem>`） |
| ExecuteNodeOperationAsync 便捷封装 | `NodeOperationOutcome<TItem>` |
