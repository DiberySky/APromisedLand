# TreeGraph.Blazor.Shared 代码清单

- 生成时间：2026-10-04 21:37:24
- 文件总数：66
- 排除：bin/、obj/、csproj、README.md
- 项目状态：TreeSky 组件闭包 + StringTree 家族（批次 1-4 已落地未提交）：Trees/{Nodes,Dialogs,Contracts,StringTree/{Models,Services,Contracts,Components,Extensions}}；组件回调边界统一 ITreeItemData<string>

## 文件 1/66 TreeGraph.Blazor.Shared/_Imports.razor

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Web
@using MudBlazor
@using MudBlazor.Extensions
@using MudBlazor.Extensions.Options
@using TreeGraph.Blazor.Shared.Common
@using TreeGraph.Blazor.Shared.Trees
@using TreeGraph.Blazor.Shared.Trees.Dialogs
@using TreeGraph.Blazor.Shared.Trees.Contracts
@using TreeGraph.Blazor.Shared.Trees.Models
@using TreeGraph.Blazor.Shared.Trees.Services
@using TreeGraph.Blazor.Shared.Trees.Navigation
```

## 文件 2/66 TreeGraph.Blazor.Shared/Common/BoolFieldSky.razor

```razor
@namespace TreeGraph.Blazor.Shared.Common

<MudField Label="@Label"
          Variant="Variant.Outlined" InnerPadding="false">
    <MudRadioGroup T="bool" Value="@Value"
                   Class="d-flex align-end pr-4"
                   ReadOnly="@ReadOnly"
                   ValueChanged="@OnValueChanged">
        <MudRadio Value="true" Label="@TrueLabel" Color="@_trueColor"/>
        <MudRadio Value="false" Label="@FalseLabel" Color="@_falseColor"/>
    </MudRadioGroup>
</MudField>

@code {
    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public bool Value { get; set; } = true;
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }

    [Parameter] public bool ReadOnly { get; set; }

    [Parameter] public string TrueLabel { get; set; } = "允许";
    [Parameter] public string FalseLabel { get; set; } = "禁止";

    Color _trueColor = Color.Default;
    Color _falseColor = Color.Default;

    protected override void OnInitialized()
    {
        _trueColor = Value ? Color.Success : Color.Default;
        _falseColor = Value ? Color.Default : Color.Warning;
    }

    private void OnValueChanged(bool value)
    {
        _trueColor = value ? Color.Success : Color.Default;
        _falseColor = value ? Color.Default : Color.Error;

        Value = value;
        _ = ValueChanged.InvokeAsync(value);
    }
}
```

## 文件 3/66 TreeGraph.Blazor.Shared/Common/DialogPageSky.razor

```razor
@namespace TreeGraph.Blazor.Shared.Common

<MudDialog TitleClass="@TitleClass"
           ContentClass="@ContentClass"
           ActionsClass="@ActionsClass">
    <TitleContent>
        @if (!string.IsNullOrEmpty(Title))
        {
            <MudToolBar Dense>
                @if (ArrowBackVisible)
                {
                    <MudIconButton Icon="@Icons.Material.Outlined.ArrowBack"
                                   Edge="Edge.Start" Color="Color.Warning"
                                   OnClick="@(e => CancelAsync())"/>
                }

                <MudSpacer/>
                <MudText>@Title</MudText>
                <MudSpacer/>

                @if (StartButtonVisible)
                {
                    <MudSpacer/>
                    <MudIconButton Icon="@Icons.Material.Outlined.Menu"
                                   Color="Color.Success"
                                   OnClick="@(e => OnStartClick.InvokeAsync())"/>
                }
            </MudToolBar>
        }
        @if (ToolBarContent != null)
        {
            <MudPaper>
                <MudToolBar Dense>
                    @ToolBarContent
                </MudToolBar>
            </MudPaper>
        }
    </TitleContent>
    <DialogContent>
        @if (DialogContent != null)
        {
            @DialogContent
        }
    </DialogContent>
    <DialogActions>
        <MudContainer MaxWidth="@MaxWidth">
            <MudStack Row Class="@ActionsContentClass">
                @if (CancelButtonVisible)
                {
                    <MudButton Variant="Variant.Text"
                               StartIcon="@Icons.Material.Filled.Close"
                               Color="Color.Default"
                               OnClick="@CancelAsync">
                        取消
                    </MudButton>
                }

                @if (SubmitButtonVisible)
                {
                    <MudButton Variant="Variant.Filled"
                               Color="Color.Primary"
                               StartIcon="@Icons.Material.Filled.SaveAs"
                               OnClick="@(() => OnSubmitClick.InvokeAsync())">
                        提交
                    </MudButton>
                }

                @if (DialogActions != null)
                {
                    @DialogActions
                }
            </MudStack>
        </MudContainer>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public required string Title { get; set; }
    [Parameter] public MaxWidth MaxWidth { get; set; } = MaxWidth.Small;

    [Parameter] public bool ArrowBackVisible { get; set; } = true;
    [Parameter] public bool CancelButtonVisible { get; set; }
    [Parameter] public bool SubmitButtonVisible { get; set; }
    [Parameter] public bool StartButtonVisible { get; set; }

    [Parameter] public string TitleClass { get; set; } = "mud-theme-primary px-0 pa-0";
    [Parameter] public string ContentClass { get; set; } = "mud-theme-dark";
    [Parameter] public string ActionsClass { get; set; } = "mud-theme-default";
    [Parameter] public string ActionsContentClass { get; set; } = "mr-8 py-3";

    [Parameter] public RenderFragment? TitleContent { get; set; }
    [Parameter] public RenderFragment? ToolBarContent { get; set; }
    [Parameter] public RenderFragment? DialogContent { get; set; }
    [Parameter] public RenderFragment? DialogActions { get; set; }

    [Parameter] public EventCallback OnStartClick { get; set; }
    [Parameter] public EventCallback OnCanceledClick { get; set; }
    [Parameter] public EventCallback OnSubmitClick { get; set; }

    private async Task CancelAsync()
    {
        MudDialog?.Close(DialogResult.Cancel());

        await OnCanceledClick.InvokeAsync();
    }
}
```

## 文件 4/66 TreeGraph.Blazor.Shared/Common/DialogSky.razor

```razor
@namespace TreeGraph.Blazor.Shared.Common

<MudDialog TitleClass="@TitleClass"
           ContentClass="@ContentClass"
           ActionsClass="@ActionsClass">
    <TitleContent>
        @if (!string.IsNullOrEmpty(Title))
        {
            <MudToolBar Dense>
                <MudSpacer/>
                <MudText Color="Color.Inherit">@Title</MudText>
                <MudSpacer/>
            </MudToolBar>
        }

        @if (ToolBarContent != null)
        {
            <MudPaper>
                <MudToolBar Dense>
                    @ToolBarContent
                </MudToolBar>
            </MudPaper>
        }
    </TitleContent>
    <DialogContent>
        @if (DialogContent != null)
        {
            @DialogContent
        }
    </DialogContent>
    <DialogActions>
        <MudContainer MaxWidth="@MaxWidth">
            <MudStack Row Class="@ActionsContentClass" Spacing="2" AlignItems="AlignItems.Center">
                @if (DialogLeftActions != null)
                {
                    @DialogLeftActions
                }

                <MudSpacer/>

                @if (DialogRightActions != null)
                {
                    @DialogRightActions
                }

                @if (SubmitButtonVisible)
                {
                    <MudButton Variant="Variant.Text"
                               Color="Color.Success"
                               StartIcon="@(IconsVisible ? Icons.Material.Filled.SaveAs : null)"
                               OnClick="@(() => OnSubmitClick.InvokeAsync())">
                        @SubmitButtonText
                    </MudButton>
                }

                @if (CancelButtonVisible)
                {
                    <MudButton Variant="Variant.Text"
                               StartIcon="@(IconsVisible ? Icons.Material.Filled.Close : null)"
                               Color="Color.Default"
                               OnClick="@CancelAsync">
                        @CancelButtonText
                    </MudButton>
                }
            </MudStack>
        </MudContainer>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public required string Title { get; set; }
    [Parameter] public bool TitleDense { get; set; } = true;

    [Parameter] public MaxWidth MaxWidth { get; set; } = MaxWidth.Small;

    [Parameter] public bool CancelButtonVisible { get; set; } = true;
    [Parameter] public bool SubmitButtonVisible { get; set; }
    [Parameter] public bool IconsVisible { get; set; } = true;

    [Parameter] public string CancelButtonText { get; set; } = "取消";
    [Parameter] public string SubmitButtonText { get; set; } = "提交";

    [Parameter] public string TitleClass { get; set; } = "mud-theme-success px-0 pa-0";
    [Parameter] public string ContentClass { get; set; } = "mud-theme-dark";
    [Parameter] public string ActionsClass { get; set; } = "mud-theme-default";
    [Parameter] public string ActionsContentClass { get; set; } = "";

    [Parameter] public RenderFragment? TitleContent { get; set; }
    [Parameter] public RenderFragment? ToolBarContent { get; set; }
    [Parameter] public RenderFragment? DialogContent { get; set; }
    [Parameter] public RenderFragment? DialogLeftActions { get; set; }
    [Parameter] public RenderFragment? DialogRightActions { get; set; }

    [Parameter] public EventCallback OnCanceledClick { get; set; }
    [Parameter] public EventCallback OnSubmitClick { get; set; }

    private async Task CancelAsync()
    {
        MudDialog?.Close(DialogResult.Cancel());

        await OnCanceledClick.InvokeAsync();
    }
}
```

## 文件 5/66 TreeGraph.Blazor.Shared/Common/ProgressCircularSky.razor

```razor
@namespace TreeGraph.Blazor.Shared.Common

@* ProgressCircularSky：加载中占位（MudProgressCircular 薄包装） *@
<MudContainer style="display: flex; justify-content: center; align-items: center; height: 300px;">
    <MudProgressCircular Color="Color.Warning" Indeterminate="true" Size="Size.Large"/>
</MudContainer>
```

## 文件 6/66 TreeGraph.Blazor.Shared/Trees/Attributes/TreeRouteAttribute.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Attributes;

/// <summary>
/// 声明泛型树 API 客户端使用的 URL 前缀。
///
/// 用途：<see cref="Services.DiberyTreeApiClient{T}"/> 用它替代 typeof(T).Name
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
```

## 文件 7/66 TreeGraph.Blazor.Shared/Trees/Contracts/NodeAction.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Contracts;
/// <summary>
/// 树节点操作类型
/// </summary>
public enum NodeAction
{
    /// <summary>查看详情</summary>
    View,
    /// <summary>创建子项</summary>
    AddChild,
    /// <summary>编辑节点</summary>
    Edit,
    /// <summary>删除节点</summary>
    Delete,
    /// <summary>移动节点</summary>
    Move,
    /// <summary>排序节点</summary>
    Sort,
    /// <summary>属性</summary>
    Attribute
}
```

## 文件 8/66 TreeGraph.Blazor.Shared/Trees/Contracts/NodeActionResult.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Contracts;
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

## 文件 9/66 TreeGraph.Blazor.Shared/Trees/Contracts/NodeOperationOutcome.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Contracts;
public class NodeOperationOutcome<TItem> where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>用户选择的操作</summary>
    public required NodeAction Action { get; set; }

    /// <summary>目标节点</summary>
    public required TItem Node { get; set; }
}
```

## 文件 10/66 TreeGraph.Blazor.Shared/Trees/Contracts/NodeTemplate.cs

```csharp
using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Contracts;
public class NodeTemplate<TItem>
    where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>目标节点</summary>
    public required ITreeItemData<TItem> Node { get; set; }

    /// <summary>用户选择的操作</summary>
    public RenderFragment<TItem>? ActionTemplate { get; set; }

    public RenderFragment<TItem>? EditTemplate { get; set; }
}
```

## 文件 11/66 TreeGraph.Blazor.Shared/Trees/Contracts/ParentSelectResult.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Contracts;
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

## 文件 12/66 TreeGraph.Blazor.Shared/Trees/Contracts/SortResult.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Contracts;
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

## 文件 13/66 TreeGraph.Blazor.Shared/Trees/Dialogs/BlazorService.cs

```csharp
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Components;
using MudBlazor.Extensions.Options;

namespace TreeGraph.Blazor.Shared.Trees.Dialogs;
/// <summary>
/// TreeSky 对话框默认选项承载（精简自源 BlazorService，仅保留 TreeSky 闭包使用的成员）。
/// </summary>
public class BlazorService
{
    public DialogOptionsEx DialogOptions { get; set; } = new DialogOptionsEx
    {
        MaximizeButton = true, // 启用最大化/还原按钮
        CloseButton = false,
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

## 文件 14/66 TreeGraph.Blazor.Shared/Trees/Dialogs/DialogConfig.cs

```csharp
using MudBlazor;

namespace TreeGraph.Blazor.Shared.Trees.Dialogs;
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

    /// <summary>是否全宽</summary>
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

## 文件 15/66 TreeGraph.Blazor.Shared/Trees/Dialogs/MessageService.cs

```csharp
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Components;
using MudBlazor.Extensions.Options;

namespace TreeGraph.Blazor.Shared.Trees.Dialogs;
/// <summary>
/// TreeSky 的消息/通知服务（精简版：基于 MudBlazor 原生 MessageBox 与 Snackbar，不依赖自定义 MessageDialog）。
/// </summary>
public class MessageService(IDialogService dialogService, ISnackbar snackbar)
{
    public DialogOptionsEx DialogOptions { get; set; } = new DialogOptionsEx
    {
        MaximizeButton = true,
        CloseButton = false,
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
    public void Details(string message, string detail)
    {
        snackbar.Add(message, Severity.Error, config =>
        {
            config.VisibleStateDuration = 3000;
            config.ShowCloseIcon = false;
            config.Action = "查看";
            config.ActionColor = Color.Info;
            config.ActionVariant = Variant.Filled;
            config.OnClick = async _ =>
            {
                await dialogService.ShowMessageBoxAsync(
                    message, detail, yesText: "确定",
                    options: new DialogOptions
                    {
                        BackdropClick = true,
                        MaxWidth = MaxWidth.ExtraSmall,
                        FullWidth = true,
                        CloseButton = false,
                    });
            };
        });
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

    public void Info(string message)
    {
        snackbar.Add(message, Severity.Info, config => { config.VisibleStateDuration = 3000; });
    }

    public void Error(string? message)
    {
        var duration = 5000;
#if DEBUG
        duration = 10000;
        if (message is { Length: > 30 })
            message = message[..30];
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

## 文件 16/66 TreeGraph.Blazor.Shared/Trees/Dialogs/TreeNodeDialogService.cs

```csharp
using MudBlazor;
using MudBlazor.Extensions;
using TreeGraph.Blazor.Shared.Trees;
using TreeGraph.Blazor.Shared.Trees.Nodes;
using TreeGraph.Blazor.Shared.Trees.Contracts;
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Dialogs;
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
        Func<TItem, IEnumerable<TItem>>? childrenAccessor = null,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "TreeItems", treeItems },
            { "CurrentNode", currentNode },
            { "CurrentParent", currentParent },
            { "AllowRootSelection", allowRoot },
            { "CanSelectNode", canSelectNode },
            { "ChildrenAccessor", childrenAccessor }
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
        var allItems = await treeSky.EnsureAllNodesLoadedAsync();
        return await ShowParentSelectDialogAsync(
            allItems, currentNode, currentParent, allowRoot, null, null, config);
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
```

## 文件 17/66 TreeGraph.Blazor.Shared/Trees/Extensions/TreeSkyServiceCollectionExtensions.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Extensions;
using TreeGraph.Blazor.Shared.Trees.Dialogs;
using TreeGraph.Blazor.Shared.Trees.Navigation;
using TreeGraph.Blazor.Shared.Trees.Services;

namespace TreeGraph.Blazor.Shared.Trees.Extensions;
public static class TreeSkyServiceCollectionExtensions
{
    /// <summary>
    /// 注册 TreeSky 组件库所需服务：
    /// BlazorService、MessageService、TreeNodeDialogService&lt;&gt;、导航历史、DiberyTreeApiClient&lt;&gt;。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="httpClientName">内部 HttpClient 名称（默认 TreeSky）</param>
    /// <param name="configureClient">可选：配置 API 基地址等</param>
    /// <remarks>
    /// 前置条件：宿主需已调用 <c>services.AddMudServices()</c>（内部 AddMudExtensions
    /// 依赖 MudBlazor 服务已注册，否则启动时抛 InvalidOperationException）。
    /// 泛型 <see cref="ITreeClientService{TTree}"/> 由宿主按具体节点类型自行注册实现。
    /// </remarks>
    public static IServiceCollection AddTreeSky(
        this IServiceCollection services,
        string httpClientName = "TreeSky",
        Action<HttpClient>? configureClient = null)
    {
        // MudBlazor.Extensions（ShowExAsync / DialogOptionsEx 运行时依赖）
        services.AddMudExtensions();

        services.AddScoped<BlazorService>();
        services.AddScoped<MessageService>();
        services.AddScoped(typeof(TreeNodeDialogService<>));
        services.AddScoped<ITreeNavigationHistoryService, TreeNavigationHistoryService>();

        // 泛型树 API 客户端（open generic，经命名 HttpClient 工厂获取 HttpClient）
        services.AddScoped(typeof(DiberyTreeApiClient<>));

        // 默认树节点写操作 Handler（宿主可注册同接口实现覆盖）
        services.AddScoped(typeof(ITreeActionHandler<>), typeof(DefaultTreeActionHandler<>));

        var builder = services.AddHttpClient(httpClientName);
        if (configureClient != null)
        {
            builder.ConfigureHttpClient(configureClient);
        }

        // 注意：必须用 AddTransient（非 TryAdd）。AddHttpClient(name) 内部已 TryAdd 注册过
        // 一个指向无名客户端的 HttpClient，TryAdd 会静默失效，导致 ApiClient 拿到
        // 没有 BaseAddress / 消息处理器的 HttpClient。
        services.AddTransient(sp =>
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName));

        return services;
    }
}
```

## 文件 18/66 TreeGraph.Blazor.Shared/Trees/Models/ApiResponse.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Models;

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

## 文件 19/66 TreeGraph.Blazor.Shared/Trees/Models/IArchivableTreeNodeBase.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Models;

/// <summary>
/// 可归档的树节点接口
/// </summary>
public interface IArchivableTreeNodeBase<TItem> : ITreeNodeBase<TItem>
{
    /// <summary>是否已归档</summary>
    bool IsArchived { get; set; }
}
```

## 文件 20/66 TreeGraph.Blazor.Shared/Trees/Models/IHierarchyTreeNodeBase.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Models;

/// <summary>
/// 有层级信息的树节点接口
/// </summary>
public interface IHierarchyTreeNodeBase<TItem> : ITreeNodeBase<TItem>
{
    /// <summary>节点深度（从0开始，根节点为0）</summary>
    int Depth { get; }
}
```

## 文件 21/66 TreeGraph.Blazor.Shared/Trees/Models/ITreeNodeBase.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Models;

public interface ITreeNodeBase<TItem>
{
    string Id { get; set; }
    string? ParentId { get; set; }
    /// <summary>节点描述</summary>
    string? Description { get; set; }
    /// <summary>是否允许有子节点</summary>
    bool CanHaveChildren { get; set; }
    int SortOrder { get; set; }

    bool HasChildren { get; set; }
    TItem? Parent { get; set; }

    string Text();
}
```

## 文件 22/66 TreeGraph.Blazor.Shared/Trees/Models/StringTreeNode.cs

```csharp
using System.Text.Json.Serialization;
using TreeGraph.Blazor.Shared.Trees.Attributes;

namespace TreeGraph.Blazor.Shared.Trees.Models;

/// <summary>
/// 库内建的“字符串节点”类型：以 <see cref="Name"/> 字符串作为显示文本，
/// 满足 TreeSky 泛型约束 class, ITreeNodeBase&lt;T&gt;, new()。
/// （System.String 为 sealed 且无无参构造，无法直接作为 TItem，故用此类承载。）
/// 宿主可直接使用，也可继承后扩展字段。
/// </summary>
/// <remarks>
/// 路由值与类型名一致：后端 StringTreeNodeController 使用 [Route("[controller]")]，
/// 显式声明后类型改名不会漂移 URL 契约。
/// </remarks>
[TreeRoute("StringTreeNode")]
public class StringTreeNode : ITreeNodeBase<StringTreeNode>
{
    public string Id { get; set; } = string.Empty;

    /// <summary>节点名称（显示字符串）</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool CanHaveChildren { get; set; } = true;

    public int SortOrder { get; set; }

    public bool HasChildren { get; set; }

    public string? ParentId { get; set; }

    /// <summary>父节点导航属性：仅前端组件内部赋值；JSON 传输时忽略，避免循环引用。</summary>
    [JsonIgnore]
    public StringTreeNode? Parent { get; set; }

    /// <summary>
    /// 子节点导航属性：仅供父节点选择对话框反射使用；
    /// JSON 传输时忽略，避免与 <see cref="Parent"/> 形成循环引用。
    /// </summary>
    [JsonIgnore]
    public List<StringTreeNode> Children { get; set; } = [];

    public string Text() => Name;
}
```

## 文件 23/66 TreeGraph.Blazor.Shared/Trees/Models/TreeNodeDto.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Models;

/// <summary>
/// 树节点数据传输对象（泛型）
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class TreeNodeDto<T>
{
    /// <summary>节点唯一标识</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>父节点ID（根节点为 null）</summary>
    public string? ParentId { get; set; }

    /// <summary>节点值（泛型）</summary>
    public T? Value { get; set; }

    /// <summary>父节点值</summary>
    public T? Parent { get; set; }

    /// <summary>显示文本</summary>
    public string? Text { get; set; }

    /// <summary>排序序号，数值越小排序越靠前</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>图标</summary>
    public string? Icon { get; set; }

    /// <summary>是否有子节点</summary>
    public bool HasChildren { get; set; }

    /// <summary>是否展开</summary>
    public bool Expanded { get; set; }

    /// <summary>是否选中</summary>
    public bool Selected { get; set; }

    /// <summary>子节点列表（完整加载时使用）</summary>
    public List<TreeNodeDto<T>>? Children { get; set; }

    /// <summary>额外数据（扩展字段）</summary>
    public Dictionary<string, object>? ExtraData { get; set; }
}
```

## 文件 24/66 TreeGraph.Blazor.Shared/Trees/Models/TreeQueryParams.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Models;

/// <summary>
/// 树节点查询参数
/// </summary>
public class TreeQueryParams
{
    /// <summary>父节点ID（根节点传 null 或空字符串）</summary>
    public string? ParentId { get; set; }

    /// <summary>搜索关键词（可选）</summary>
    public string? SearchTerm { get; set; }

    /// <summary>是否只加载有子节点的节点</summary>
    public bool OnlyWithChildren { get; set; }

    /// <summary>最大深度（0表示不限制）</summary>
    public int MaxDepth { get; set; }

    /// <summary>分页参数 - 页码</summary>
    public int Page { get; set; } = 1;

    /// <summary>分页参数 - 每页大小</summary>
    public int PageSize { get; set; } = 100;
}
```

## 文件 25/66 TreeGraph.Blazor.Shared/Trees/Navigation/HistoryEntry.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Navigation;

public class HistoryEntry
{
    public string Url { get; set; } = string.Empty;
    public string? RootId { get; set; }
    public string? ClickNodeId { get; set; }
}
```

## 文件 26/66 TreeGraph.Blazor.Shared/Trees/Navigation/ITreeNavigationHistoryService.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Navigation;

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

## 文件 27/66 TreeGraph.Blazor.Shared/Trees/Navigation/TreeNavigationHistoryService.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.Navigation;

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
            return entry;
        }

        return new HistoryEntry { Url = defaultUrl };
    }

    public void Clear() => _stack.Clear();
}
```

## 文件 28/66 TreeGraph.Blazor.Shared/Trees/Nodes/DialogTreeSky.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.Nodes
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>, new()

@* 精简移植版：移除文件/图片/视频/位置附件按钮（附件子系统未包含） *@
<DialogSky Title="@Title"
           CancelButtonVisible="@CancelButtonVisible"
           SubmitButtonVisible="@SubmitButtonVisible"
           OnCanceledClick="e => OnCanceledClick.InvokeAsync()"
           OnSubmitClick="e => OnSubmitClick.InvokeAsync()">
    <ToolBarContent>
        @if (ToolBarContent != null)
        {
            @ToolBarContent
        }
    </ToolBarContent>
    <DialogContent>
        @if (DialogContent != null)
        {
            @DialogContent
        }
    </DialogContent>
    <DialogRightActions>
        @if (DialogActions != null)
        {
            @DialogActions
        }
    </DialogRightActions>
</DialogSky>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public required string Title { get; set; }
    [Parameter] public required TItem Item { get; set; }

    [Parameter] public bool CancelButtonVisible { get; set; } = true;
    [Parameter] public bool SubmitButtonVisible { get; set; }

    [Parameter] public RenderFragment? TitleContent { get; set; }
    [Parameter] public RenderFragment? ToolBarContent { get; set; }
    [Parameter] public RenderFragment? DialogContent { get; set; }
    [Parameter] public RenderFragment? DialogActions { get; set; }

    [Parameter] public EventCallback OnCanceledClick { get; set; }
    [Parameter] public EventCallback OnSubmitClick { get; set; }
}
```

## 文件 29/66 TreeGraph.Blazor.Shared/Trees/Nodes/TreeNodeActionsDialog.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.Nodes
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>, new()

@implements IDisposable

<DialogSky Title="节点操作">
    <ToolBarContent>
        @if (IsBoot)
        {
            <MudText Color="Color.Info">根结点</MudText>
        }
        else
        {
            <MudText Color="Color.Info">@NodeTemplate.Node.Value!.Parent?.Text()</MudText>
        }
        <MudSpacer/>
        @if (!IsBoot)
        {
            <MudButton Variant="Variant.Text"
                       StartIcon="@Icons.Material.Outlined.MoveUp"
                       Color="Color.Warning"
                       OnClick="@OnMove">
                移动
            </MudButton>
        }
    </ToolBarContent>
    <DialogContent>
        <MudContainer Class="py-4" MaxWidth="MaxWidth.Medium">
            <MudStack Spacing="2">

                <MudTextField T="string"
                              Value="NodeTemplate.Node.Value!.Text()"
                              Label="名称"
                              Variant="Variant.Outlined"
                              ReadOnly="true"/>

                <MudTextField T="string"
                              Value="NodeTemplate.Node.Value!.Description"
                              Label="描述"
                              Variant="Variant.Outlined"
                              ReadOnly="true"
                              Lines="3"/>

                <BoolFieldSky Label="是否允许添加子节点" ReadOnly="true"
                              @bind-Value="NodeTemplate.Node.Value!.CanHaveChildren" />

            </MudStack>
        </MudContainer>
    </DialogContent>
    <DialogLeftActions>
        @if (!NodeTemplate.Node.HasChildren)
        {
            <MudButton OnClick="OnDelete"
                       Variant="Variant.Text"
                       Color="Color.Error"
                       StartIcon="@Icons.Material.Outlined.Delete">
                删除
            </MudButton>
        }
    </DialogLeftActions>
    <DialogRightActions>
        @if (NodeTemplate.Node.Value!.CanHaveChildren
             || NodeTemplate.Node.Children?.Count > 1)
        {
            <MudButton OnClick="OnSort"
                       Variant="Variant.Text"
                       Color="Color.Default"
                       StartIcon="@Icons.Material.Outlined.ImportExport">
                子项排序
            </MudButton>
        }

        @if (!IsBoot)
        {
            <MudButton OnClick="OnEdit"
                       Variant="Variant.Text"
                       Color="Color.Primary"
                       StartIcon="@Icons.Material.Outlined.Edit">
                设置修改
            </MudButton>

        }

        @if (NodeTemplate.Node.Value!.CanHaveChildren)
        {
            <MudButton OnClick="OnAddChild"
                       Variant="Variant.Text"
                       Color="Color.Success"
                       StartIcon="@Icons.Material.Outlined.Add">
                创建子项
            </MudButton>
        }
    </DialogRightActions>
</DialogSky>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public required NodeTemplate<TItem> NodeTemplate { get; set; }
    [Parameter] public TItem? ParentNode { get; set; }
    [Parameter] public bool IsBoot { get; set; }

    private void OnAttribute()
    {
        CloseWithAction(NodeAction.Attribute);
    }

    private void OnAddChild()
    {
        CloseWithAction(NodeAction.AddChild);
    }

    private void OnEdit()
    {
        CloseWithAction(NodeAction.Edit);
    }

    private void OnDelete()
    {
        CloseWithAction(NodeAction.Delete);
    }

    private void OnMove()
    {
        CloseWithAction(NodeAction.Move);
    }

    private void OnSort()
    {
        CloseWithAction(NodeAction.Sort);
    }

    private void CloseWithAction(NodeAction action)
    {
        MudDialog?.Close(DialogResult.Ok(new NodeActionResult<TItem>
        {
            Action = action,
            Node = NodeTemplate.Node.Value!
        }));
    }

    private void Close()
    {
        MudDialog?.Close(DialogResult.Cancel());
    }

    public void Dispose()
    {
    }
}
```

## 文件 30/66 TreeGraph.Blazor.Shared/Trees/Nodes/TreeNodeEditDialog.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.Nodes
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>, new()

@* 精简移植版：移除节点属性区（NodeAttributeItemsSky/属性定义选择），仅保留 EditTemplate 表单 *@
@implements IDisposable

<DialogTreeSky TItem="@TItem"
               Item="@_node"
               Title="@_title"
               SubmitButtonVisible="true"
               OnSubmitClick="@SubmitAsync"
               OnCanceledClick="@Close">
    <DialogContent>
        <MudContainer Class="py-8">
            <MudForm @ref="_form" Model="@_node">
                @if (NodeTemplate.EditTemplate is null)
                {
                    <MudStack Spacing="3">
                        <MudText Color="Color.Secondary">未提供编辑模板（EditTemplate）。</MudText>
                    </MudStack>
                }
                else
                {
                    @NodeTemplate.EditTemplate(_node)
                }
            </MudForm>
        </MudContainer>
    </DialogContent>
</DialogTreeSky>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public bool IsCreate { get; set; }

    [Parameter] public required NodeTemplate<TItem> NodeTemplate { get; set; }

    private MudForm _form = null!;

    private TItem _node = null!;

    private string _title = null!;

    protected override void OnInitialized()
    {
        _node = NodeTemplate.Node.Value!;

        _title = IsCreate ? $"创建【{_node.Parent!.Text()}】子节点" : _node.Text();

        base.OnInitialized();
    }

    private async Task SubmitAsync()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid) return;

        MudDialog?.Close(DialogResult.Ok(_node));
    }

    private void Close()
    {
        MudDialog?.Close(DialogResult.Cancel());
    }

    public void Dispose()
    {
    }
}
```

## 文件 31/66 TreeGraph.Blazor.Shared/Trees/Nodes/TreeNodeParentSelectDialog.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.Nodes
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>
@implements IDisposable

<MudDialog>
    <DialogContent>
        <MudContainer Class="py-2" Style="min-height: 300px; max-height: 500px; overflow-y: auto;">
            <MudAlert Severity="Severity.Info"
                      Dense="true"
                      Class="mb-2"
                      ShowCloseIcon="false">
                当前选中: <strong>@SelectedName</strong>
                @if (SelectedParent == null)
                {
                    <MudText Inline="true" Color="Color.Secondary">(根节点)</MudText>
                }
            </MudAlert>

            <MudTextField T="string"
                          @bind-Value="_searchText"
                          Label="搜索节点"
                          Variant="Variant.Outlined"
                          Adornment="Adornment.Start"
                          AdornmentIcon="@Icons.Material.Filled.Search"
                          Class="mb-2"
                          Clearable="true"
                          DebounceInterval="300"
                          OnDebounceIntervalElapsed="OnSearchAsync" />

            @if (_isLoading)
            {
                <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
            }
            else if (_filteredItems?.Any() != true && !string.IsNullOrEmpty(_searchText))
            {
                <MudText Align="Align.Center" Color="Color.Secondary" Class="my-4">
                    未找到匹配的节点
                </MudText>
            }
            else
            {
                <MudTreeView T="TItem"
                             @bind-SelectedValue="_selectedValue"
                             SelectionMode="SelectionMode.SingleSelection"
                             ExpandOnClick="true">
                    @RenderTreeItems(_filteredItems ?? _treeItems)
                </MudTreeView>
            }
        </MudContainer>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Close"
                   Variant="Variant.Text"
                   Color="Color.Default">
            取消
        </MudButton>
        <MudButton OnClick="ConfirmSelection"
                   Variant="Variant.Filled"
                   Color="Color.Primary"
                   Disabled="!CanConfirm">
            确认选择
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public required List<TItem> TreeItems { get; set; }
    [Parameter] public TItem? CurrentNode { get; set; }
    [Parameter] public TItem? CurrentParent { get; set; }
    [Parameter] public bool AllowRootSelection { get; set; } = true;
    [Parameter] public Func<TItem, bool>? CanSelectNode { get; set; }

    /// <summary>子节点访问器（可选）。不传时使用反射读取 TItem 的 "Children" 属性。</summary>
    [Parameter] public Func<TItem, IEnumerable<TItem>>? ChildrenAccessor { get; set; }

    private string _searchText = string.Empty;
    private bool _isLoading = false;

    private List<TItem> _treeItems = new();
    private List<TItem>? _filteredItems;
    private TItem? _selectedValue;
    private TItem? SelectedParent => _selectedValue;

    private string SelectedName => SelectedParent?.Text() ?? CurrentParent?.Text() ?? "根节点";
    private bool CanConfirm => _selectedValue != null || AllowRootSelection;

    protected override void OnInitialized()
    {
        _treeItems = TreeItems.ToList();
        _selectedValue = CurrentParent;
        base.OnInitialized();
    }

    private RenderFragment RenderTreeItems(List<TItem> items)
    {
        return builder =>
        {
            foreach (var item in items)
            {
                var isDisabled = IsNodeDisabled(item);
                var isSelected = item.Id == _selectedValue?.Id;

                builder.OpenComponent<MudTreeViewItem<TItem>>(0);
                builder.AddAttribute(1, "Value", item);
                builder.AddAttribute(2, "Text", item.Text());
                builder.AddAttribute(3, "Icon", GetNodeIcon(item, isSelected));
                builder.AddAttribute(4, "IconColor", GetNodeColor(item, isSelected));
                builder.AddAttribute(5, "Disabled", isDisabled);
                builder.AddAttribute(6, "OnClick", EventCallback.Factory.Create<TItem>(this, OnNodeClick));

                // 递归渲染子节点
                var children = GetChildren(item);
                if (children.Any())
                {
                    builder.AddAttribute(7, "ChildContent", RenderTreeItems(children));
                }

                builder.CloseComponent();
            }
        };
    }

    private void OnNodeClick(TItem node)
    {
        if (IsNodeDisabled(node)) return;
        _selectedValue = node;
        StateHasChanged();
    }

    private bool IsNodeDisabled(TItem node)
    {
        if (CurrentNode != null && IsDescendantOrSelf(node, CurrentNode))
            return true;

        if (CanSelectNode != null && !CanSelectNode(node))
            return true;

        return false;
    }

    private bool IsDescendantOrSelf(TItem node, TItem target)
    {
        if (node.Id == target.Id) return true;

        var children = GetChildren(node);
        if (!children.Any()) return false;

        foreach (var child in children)
        {
            if (IsDescendantOrSelf(child, target))
                return true;
        }

        return false;
    }

    private List<TItem> GetChildren(TItem node)
    {
        // 1. 显式访问器优先
        if (ChildrenAccessor is not null)
            return ChildrenAccessor(node).ToList();

        // 2. 反射兜底
        var childrenProperty = node.GetType().GetProperty("Children");
        if (childrenProperty == null) return new List<TItem>();

        var children = childrenProperty.GetValue(node) as IEnumerable<object>;
        if (children == null) return new List<TItem>();

        return children.OfType<TItem>().ToList();
    }

    private string GetNodeIcon(TItem node, bool isSelected)
    {
        if (isSelected) return Icons.Material.Filled.CheckCircle;
        if (GetChildren(node).Any() || node.HasChildren) return Icons.Material.Filled.Folder;
        return Icons.Material.Filled.InsertDriveFile;
    }

    private Color GetNodeColor(TItem node, bool isSelected)
    {
        if (isSelected) return Color.Primary;
        if (IsNodeDisabled(node)) return Color.Default;
        return Color.Inherit;
    }

    private async Task OnSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            _filteredItems = null;
            return;
        }

        _isLoading = true;
        StateHasChanged();

        await Task.Delay(100);

        var keyword = _searchText.ToLowerInvariant();
        var matchedIds = _treeItems
            .Where(n => n.Text().ToLowerInvariant().Contains(keyword))
            .Select(n => n.Id)
            .ToHashSet();

        var result = new HashSet<string>();
        foreach (var id in matchedIds)
        {
            AddWithAncestors(id, result);
        }

        _filteredItems = _treeItems.Where(n => result.Contains(n.Id)).ToList();

        _isLoading = false;
        StateHasChanged();
    }

    private void AddWithAncestors(string nodeId, HashSet<string> result)
    {
        result.Add(nodeId);
        var node = _treeItems.FirstOrDefault(n => n.Id == nodeId);
        if (node?.ParentId != null)
        {
            AddWithAncestors(node.ParentId, result);
        }
    }

    private void ConfirmSelection()
    {
        var result = new ParentSelectResult<TItem>
        {
            IsConfirmed = true,
            SelectedParent = SelectedParent
        };
        MudDialog?.Close(DialogResult.Ok(result));
    }

    private void Close()
    {
        MudDialog?.Close(DialogResult.Cancel());
    }

    public void Dispose()
    {
    }
}
```

## 文件 32/66 TreeGraph.Blazor.Shared/Trees/Nodes/TreeNodeSortDialog.razor

```razor
@* TreeNodeSortDialog.razor *@
@namespace TreeGraph.Blazor.Shared.Trees.Nodes
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>, new()

<DialogSky Title="拖拽排序" SubmitButtonVisible
           OnSubmitClick="@Submit">
    <DialogContent>
        <MudList T="string" SelectionMode="SelectionMode.SingleSelection">
            @for (int i = 0; i < _children.Count; i++)
            {
                int index = i;
                <MudListItem T="string">
                    <MudContainer class="@("d-flex align-items-center")">
                        <MudIconButton Icon="@Icons.Material.Outlined.ArrowUpward"
                                       Class="mr-2"
                                       Size="Size.Small"
                                       Color="Color.Info"
                                       Disabled="@(index == 0)"
                                       OnClick="@(() => Move(index, -1))" />

                        <MudText Typo="Typo.body1">@_children[index].Text()</MudText>
                        <MudSpacer />
                        <MudIconButton Icon="@Icons.Material.Outlined.ArrowDownward"
                                       Size="Size.Small"
                                       Color="Color.Info"
                                       Disabled="@(index == _children.Count - 1)"
                                       OnClick="@(() => Move(index, 1))"/>
                    </MudContainer>
                </MudListItem>
            }
        </MudList>
    </DialogContent>
</DialogSky>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public required ITreeItemData<TItem> Node { get; set; }
    private List<TItem> _children = [];

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Node.Children 可能为 null（节点未展开）；x.Value 可能为 null（懒加载占位）
        _children = Node.Children?
            .Where(x => x.Value is not null)
            .Select(x => x.Value!)
            .ToList()
            ?? new List<TItem>();
    }

    private void Move(int currentIndex, int offset)
    {
        int newIndex = currentIndex + offset;
        if (newIndex < 0 || newIndex >= _children.Count)
            return;

        (_children[currentIndex], _children[newIndex]) = (_children[newIndex], _children[currentIndex]);
    }

    private void Submit()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].SortOrder = i;
        }

        MudDialog?.Close(DialogResult.Ok(_children));
    }
}
```

## 文件 33/66 TreeGraph.Blazor.Shared/Trees/Nodes/TreeNodeViewDialog.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.Nodes
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>
@implements IDisposable

<MudDialog>
    <DialogContent>
        <MudContainer Class="py-4" MaxWidth="MaxWidth.Small">
            <MudGrid Spacing="2">
                <MudItem xs="12">
                    <MudField Label="ID" Variant="Variant.Text">@Node.Id</MudField>
                </MudItem>
                <MudItem xs="12">
                    <MudField Label="名称" Variant="Variant.Outlined">@Node.Text()</MudField>
                </MudItem>
                <MudItem xs="12">
                    <MudField Label="描述" Variant="Variant.Outlined">
                        @(Node.Description ?? "-")
                    </MudField>
                </MudItem>
                <MudItem xs="12" sm="6">
                    <MudField Label="父分类" Variant="Variant.Text">@ParentName</MudField>
                </MudItem>
                <MudItem xs="12" sm="6">
                    <MudField Label="状态" Variant="Variant.Text">
                        <MudChip T="string"
                                 Color="Node.CanHaveChildren ? Color.Success : Color.Default"
                                 Size="Size.Small">
                            @(Node.CanHaveChildren ? "启用" : "禁用")
                        </MudChip>
                    </MudField>
                </MudItem>
                <MudItem xs="12" sm="6">
                    <MudField Label="排序" Variant="Variant.Text">@Node.SortOrder</MudField>
                </MudItem>
                <MudItem xs="12" sm="6">
                    <MudField Label="创建时间" Variant="Variant.Text">@(CreatedAt ?? "未知")</MudField>
                </MudItem>
            </MudGrid>
        </MudContainer>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Close"
                   Variant="Variant.Filled"
                   Color="Color.Primary">
            确定
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public required TItem Node { get; set; }
    [Parameter] public TItem? ParentNode { get; set; }
    [Parameter] public string? CreatedAt { get; set; }

    private string ParentName => ParentNode?.Text() ?? "根分类";

    private void Close()
    {
        MudDialog?.Close(DialogResult.Ok(true));
    }

    public void Dispose()
    {
    }
}
```

## 文件 34/66 TreeGraph.Blazor.Shared/Trees/Services/DefaultTreeActionHandler.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Services;

/// <summary>
/// 默认写操作实现：直接转发到 <see cref="DiberyTreeApiClient{T}"/>。
///
/// 核心价值：集中"如何从 TItem 构造 TreeNodeDto"的业务知识
/// （此前散落在 TreeSky.HandleXxxAsync 中）。
///
/// 宿主可继承覆盖 <see cref="BuildDto"/> 等方法，或整体替换为自定义 Handler。
/// </summary>
public class DefaultTreeActionHandler<TItem>(
    DiberyTreeApiClient<TItem> client)
    : ITreeActionHandler<TItem>
    where TItem : class, ITreeNodeBase<TItem>, new()
{
    // ============================================================
    // 写操作
    // ============================================================

    public virtual async Task<TItem?> CreateChildAsync(
        TItem parent, TItem newChild, CancellationToken ct = default)
    {
        var dto = BuildDto(newChild, parentId: parent.Id);
        var result = await client.CreateNodeAsync(dto, ct);
        return result.Value;
    }

    public virtual async Task<TItem?> UpdateNodeAsync(
        TItem node, CancellationToken ct = default)
    {
        var dto = BuildDto(node, parentId: node.ParentId);
        var result = await client.UpdateNodeAsync(node.Id, dto, ct);
        return result.Value;
    }

    public virtual Task<bool> DeleteNodeAsync(
        TItem node, CancellationToken ct = default)
        => client.DeleteNodeAsync(node.Id, ct);

    public virtual Task<bool> MoveNodeAsync(
        TItem node, TItem? newParent, CancellationToken ct = default)
        => client.MoveNodeAsync(node.Id, newParent?.Id, ct);

    public virtual async Task<bool> SortChildrenAsync(
        TItem parent, IReadOnlyList<TItem> orderedChildren,
        CancellationToken ct = default)
    {
        var dto = new TreeNodeDto<TItem>
        {
            Id = parent.Id,
            Text = parent.Text(),
            Icon = TreeHelper.TreeItemIcons,
            ParentId = parent.ParentId,
            Value = parent,
            Children = orderedChildren
                .Select((child, index) => new TreeNodeDto<TItem>
                {
                    Id = child.Id,
                    Text = child.Text(),
                    Icon = TreeHelper.TreeItemIcons,
                    ParentId = parent.Id,
                    SortOrder = index,
                    Value = child,
                })
                .ToList(),
        };

        await client.UpdateChildrenAsync(dto, ct);
        return true;
    }

    // ============================================================
    // 可覆盖钩子
    // ============================================================

    /// <summary>
    /// 从 TItem 构造 TreeNodeDto（默认设置 Id / Text / Icon / ParentId / SortOrder / Value）。
    /// 宿主可覆盖以追加业务字段（如 ExtraData）。
    /// </summary>
    protected virtual TreeNodeDto<TItem> BuildDto(TItem node, string? parentId)
        => new()
        {
            Id = node.Id,
            Text = node.Text(),
            Icon = TreeHelper.TreeItemIcons,
            ParentId = parentId,
            SortOrder = node.SortOrder,
            Value = node,
        };
}
```

## 文件 35/66 TreeGraph.Blazor.Shared/Trees/Services/DiberyTreeApiClient.cs

```csharp
// DiberyTreeApiClient.cs
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using TreeGraph.Blazor.Shared.Trees.Attributes;
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Services;

/// <summary>
/// 泛型树 API 客户端，用于调用后端的 TreeControllerBase&lt;T&gt;，包含树节点 CRUD。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class DiberyTreeApiClient<T>(HttpClient httpClient)
{
    /// <summary>
    /// URL 前缀。优先读类型上的 <see cref="TreeRouteAttribute"/>；
    /// 未标注则回退到 typeof(T).Name（与后端 [Route("[controller]")] 约定一致）。
    /// </summary>
    // 例如标注 [TreeRoute("StringTreeNode")] 或未标注时回退 "StringTreeNode"
    private readonly string _basePath =
        typeof(T).GetCustomAttribute<TreeRouteAttribute>()?.Route
        ?? typeof(T).Name;

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

        var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
            throw new HttpRequestException($"请求失败: {errorResponse.Message}", null, response.StatusCode);
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
}
```

## 文件 36/66 TreeGraph.Blazor.Shared/Trees/Services/ITreeActionHandler.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Services;

/// <summary>
/// 树节点写操作接口。
///
/// 职责边界：
///   - Handler = DTO 构造 + 调 API + 业务校验
///   - TreeSky = 对话框编排 + 消息提示 + UI 刷新 + CancellationToken
///
/// 与 <see cref="ITreeClientService{TTree}"/> 的关系：
///   - ITreeClientService = 读操作（宿主实现的通用契约）
///   - ITreeActionHandler  = 写操作（可替换的业务契约）
///
/// 默认实现 <see cref="DefaultTreeActionHandler{TItem}"/> 直接转发到
/// <see cref="DiberyTreeApiClient{T}"/>；宿主可替换为带校验 / 审计 / 缓存的实现。
/// </summary>
/// <typeparam name="TItem">树节点值类型</typeparam>
public interface ITreeActionHandler<TItem>
    where TItem : class, ITreeNodeBase<TItem>, new()
{
    /// <summary>创建子节点。返回创建后的节点（含后端生成的 ID 等）；失败返回 null。</summary>
    Task<TItem?> CreateChildAsync(
        TItem parent, TItem newChild, CancellationToken ct = default);

    /// <summary>更新节点信息（不含 ParentId，移动请走 <see cref="MoveNodeAsync"/>）。</summary>
    Task<TItem?> UpdateNodeAsync(
        TItem node, CancellationToken ct = default);

    /// <summary>删除节点（含全部后代）。节点不存在返回 false，其他失败抛异常。</summary>
    Task<bool> DeleteNodeAsync(
        TItem node, CancellationToken ct = default);

    /// <summary>移动节点到新父（null 表示根）。节点/新父不存在或构成环时返回 false。</summary>
    Task<bool> MoveNodeAsync(
        TItem node, TItem? newParent, CancellationToken ct = default);

    /// <summary>
    /// 按 <paramref name="orderedChildren"/> 的列表顺序重排父节点的子节点。
    /// 列表顺序即新顺序（后端按位置重新编号 SortOrder）。
    /// </summary>
    Task<bool> SortChildrenAsync(
        TItem parent, IReadOnlyList<TItem> orderedChildren, CancellationToken ct = default);
}
```

## 文件 37/66 TreeGraph.Blazor.Shared/Trees/Services/ITreeClientService.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees.Services;

public interface ITreeClientService<TTree> where TTree : class
{
    string Title { get; set; }
    bool NewPageShow { get; set; }
    bool SelectLeaf { get; set; }

    Task<IReadOnlyList<TreeNodeDto<TTree>>> LoadInitialDataAsync(
        string? rootId, CancellationToken ct = default);
    Task<IReadOnlyList<TreeNodeDto<TTree>>> LoadChildrenAsync(
        TTree? parent = null, CancellationToken ct = default);
    Task<List<string>?> GetAncestorPathFromApiAsync(
        string nodeId, CancellationToken ct = default);
}
```

## 文件 38/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringNodeActionsDialog.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components
@using TreeGraph.Blazor.Shared.Common
@using TreeGraph.Blazor.Shared.Trees.StringTree.Contracts
@using TreeGraph.Blazor.Shared.Trees.StringTree.Models

<DialogSky Title="节点操作">
    <ToolBarContent>
        @if (IsBoot)
        {
            <MudText Color="Color.Info">根结点</MudText>
        }
        else if (ParentNode is not null)
        {
            <MudText Color="Color.Info">@ParentNode.Text</MudText>
        }
        <MudSpacer/>
        @if (!IsBoot)
        {
            <MudButton Variant="Variant.Text"
                       StartIcon="@Icons.Material.Outlined.MoveUp"
                       Color="Color.Warning"
                       OnClick="@OnMove">
                移动
            </MudButton>
        }
    </ToolBarContent>
    <DialogContent>
        <MudContainer Class="py-4" MaxWidth="MaxWidth.Medium">
            <MudStack Spacing="2">
                <MudTextField T="string"
                              Value="@Node.Text"
                              Label="名称"
                              Variant="Variant.Outlined"
                              ReadOnly="true"/>

                <MudTextField T="string"
                              Value="@Node.Description"
                              Label="描述"
                              Variant="Variant.Outlined"
                              ReadOnly="true"
                              Lines="3"/>

                <BoolFieldSky Label="是否允许添加子节点"
                              ReadOnly="true"
                              @bind-Value="Node.CanHaveChildren" />
            </MudStack>
        </MudContainer>
    </DialogContent>
    <DialogLeftActions>
        @if (!HasChildren)
        {
            <MudButton OnClick="OnDelete"
                       Variant="Variant.Text"
                       Color="Color.Error"
                       StartIcon="@Icons.Material.Outlined.Delete">
                删除
            </MudButton>
        }
    </DialogLeftActions>
    <DialogRightActions>
        @if (Node.CanHaveChildren || HasChildren)
        {
            <MudButton OnClick="OnSort"
                       Variant="Variant.Text"
                       Color="Color.Default"
                       StartIcon="@Icons.Material.Outlined.ImportExport">
                子项排序
            </MudButton>
        }

        @if (!IsBoot)
        {
            <MudButton OnClick="OnEdit"
                       Variant="Variant.Text"
                       Color="Color.Primary"
                       StartIcon="@Icons.Material.Outlined.Edit">
                设置修改
            </MudButton>
        }

        @if (Node.CanHaveChildren)
        {
            <MudButton OnClick="OnAddChild"
                       Variant="Variant.Text"
                       Color="Color.Success"
                       StartIcon="@Icons.Material.Outlined.Add">
                创建子项
            </MudButton>
        }
    </DialogRightActions>
</DialogSky>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter, EditorRequired] public StringNodeMeta Node { get; set; } = null!;
    [Parameter] public StringNodeMeta? ParentNode { get; set; }
    [Parameter] public bool IsBoot { get; set; }
    [Parameter] public bool HasChildren { get; set; }

    private void OnAddChild() => CloseWith(StringNodeAction.AddChild);
    private void OnEdit() => CloseWith(StringNodeAction.Edit);
    private void OnDelete() => CloseWith(StringNodeAction.Delete);
    private void OnMove() => CloseWith(StringNodeAction.Move);
    private void OnSort() => CloseWith(StringNodeAction.Sort);

    private void CloseWith(StringNodeAction action)
    {
        MudDialog?.Close(DialogResult.Ok(new StringNodeActionResult
        {
            Action = action,
            Node = Node,
        }));
    }
}
```

## 文件 39/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringNodeEditDialog.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components
@using TreeGraph.Blazor.Shared.Common
@using TreeGraph.Blazor.Shared.Trees.StringTree.Contracts
@using TreeGraph.Blazor.Shared.Trees.StringTree.Models

<DialogSky Title="@_title"
           SubmitButtonVisible="true"
           OnSubmitClick="@SubmitAsync"
           OnCanceledClick="@Close">
    <DialogContent>
        <MudContainer Class="py-8">
            <MudForm @ref="_form" Model="@_node">
                @if (Template.EditTemplate is null)
                {
                    <MudStack Spacing="3">
                        <MudText Color="Color.Secondary">
                            未提供编辑模板（EditTemplate）。
                        </MudText>
                    </MudStack>
                }
                else
                {
                    @Template.EditTemplate(_node)
                }
            </MudForm>
        </MudContainer>
    </DialogContent>
</DialogSky>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter, EditorRequired] public StringNodeTemplate Template { get; set; } = null!;
    [Parameter] public StringNodeMeta? ParentNode { get; set; }
    [Parameter] public bool IsCreate { get; set; }

    private MudForm _form = null!;
    private StringNodeMeta _node = null!;
    private string _title = "";

    protected override void OnInitialized()
    {
        // 编辑流程克隆输入对象，取消时不影响原节点
        _node = Template.Node.Clone();

        if (IsCreate)
        {
            if (ParentNode is not null)
                _node.ParentId = ParentNode.Id;

            _title = ParentNode is not null
                ? $"创建【{ParentNode.Text}】子节点"
                : "创建根节点";
        }
        else
        {
            _title = string.IsNullOrEmpty(_node.Text) ? "编辑节点" : _node.Text;
        }
    }

    private async Task SubmitAsync()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid) return;

        MudDialog?.Close(DialogResult.Ok(_node));
    }

    private void Close()
    {
        MudDialog?.Close(DialogResult.Cancel());
    }
}
```

## 文件 40/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringNodeSortDialog.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components
@using TreeGraph.Blazor.Shared.Common
@using TreeGraph.Blazor.Shared.Trees.StringTree.Models

<DialogSky Title="拖拽排序" SubmitButtonVisible
           OnSubmitClick="@Submit">
    <DialogContent>
        <MudList T="string" SelectionMode="SelectionMode.SingleSelection">
            @for (int i = 0; i < _children.Count; i++)
            {
                int index = i;
                <MudListItem T="string">
                    <MudContainer class="d-flex align-items-center">
                        <MudIconButton Icon="@Icons.Material.Outlined.ArrowUpward"
                                       Class="mr-2"
                                       Size="Size.Small"
                                       Color="Color.Info"
                                       Disabled="@(index == 0)"
                                       OnClick="@(() => Move(index, -1))" />

                        <MudText Typo="Typo.body1">@_children[index].Text</MudText>
                        <MudSpacer />
                        <MudIconButton Icon="@Icons.Material.Outlined.ArrowDownward"
                                       Size="Size.Small"
                                       Color="Color.Info"
                                       Disabled="@(index == _children.Count - 1)"
                                       OnClick="@(() => Move(index, 1))"/>
                    </MudContainer>
                </MudListItem>
            }
        </MudList>
    </DialogContent>
</DialogSky>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    /// <summary>有序子节点列表（顺序即初始显示顺序）。</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<StringNodeMeta> Children { get; set; } = Array.Empty<StringNodeMeta>();

    private List<StringNodeMeta> _children = new();

    protected override void OnInitialized()
    {
        _children = Children
            .OrderBy(c => c.SortOrder)
            .ToList();
    }

    private void Move(int currentIndex, int offset)
    {
        int newIndex = currentIndex + offset;
        if (newIndex < 0 || newIndex >= _children.Count)
            return;

        (_children[currentIndex], _children[newIndex]) =
            (_children[newIndex], _children[currentIndex]);
    }

    private void Submit()
    {
        // ★ 列表顺序即新顺序，后端按位置重新编号 SortOrder。
        //   不要用 Select(i => i.SortOrder) 排序（会撤销用户拖拽结果）。
        var orderedIds = _children.Select(c => c.Id).ToList();
        MudDialog?.Close(DialogResult.Ok(orderedIds));
    }
}
```

## 文件 41/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringNodeViewDialog.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components
@using TreeGraph.Blazor.Shared.Trees.StringTree.Models

<MudDialog>
    <DialogContent>
        <MudContainer Class="py-4" MaxWidth="MaxWidth.Small">
            <MudGrid Spacing="2">
                <MudItem xs="12">
                    <MudField Label="ID" Variant="Variant.Text">
                        <code>@Node.Id</code>
                    </MudField>
                </MudItem>
                <MudItem xs="12">
                    <MudField Label="名称" Variant="Variant.Outlined">
                        @Node.Text
                    </MudField>
                </MudItem>
                <MudItem xs="12">
                    <MudField Label="描述" Variant="Variant.Outlined">
                        @(string.IsNullOrEmpty(Node.Description) ? "-" : Node.Description)
                    </MudField>
                </MudItem>
                <MudItem xs="12" sm="6">
                    <MudField Label="父节点" Variant="Variant.Text">
                        @(ParentNode?.Text ?? "根分类")
                    </MudField>
                </MudItem>
                <MudItem xs="12" sm="6">
                    <MudField Label="允许子节点" Variant="Variant.Text">
                        <MudChip T="string"
                                 Color="@(Node.CanHaveChildren ? Color.Success : Color.Default)"
                                 Size="Size.Small">
                            @(Node.CanHaveChildren ? "启用" : "禁用")
                        </MudChip>
                    </MudField>
                </MudItem>
                <MudItem xs="12" sm="6">
                    <MudField Label="排序" Variant="Variant.Text">@Node.SortOrder</MudField>
                </MudItem>
                <MudItem xs="12" sm="6">
                    <MudField Label="有子节点" Variant="Variant.Text">
                        @(Node.HasChildren ? "是" : "否")
                    </MudField>
                </MudItem>
            </MudGrid>
        </MudContainer>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Close"
                   Variant="Variant.Filled"
                   Color="Color.Primary">
            确定
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter, EditorRequired] public StringNodeMeta Node { get; set; } = null!;
    [Parameter] public StringNodeMeta? ParentNode { get; set; }

    private void Close()
    {
        MudDialog?.Close(DialogResult.Ok(true));
    }
}
```

## 文件 42/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringParentSelectDialog.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components
@using TreeGraph.Blazor.Shared.Trees.StringTree.Contracts
@using TreeGraph.Blazor.Shared.Trees.StringTree.Models

<MudDialog>
    <DialogContent>
        <MudContainer Class="py-2"
                      Style="min-height: 300px; max-height: 500px; overflow-y: auto;">
            <MudAlert Severity="Severity.Info"
                      Dense="true"
                      Class="mb-2"
                      ShowCloseIcon="false">
                当前选中:
                <strong>@SelectedName</strong>
                @if (_selectedId is null)
                {
                    <MudText Inline="true" Color="Color.Secondary">(根节点)</MudText>
                }
            </MudAlert>

            <MudTextField T="string"
                          @bind-Value="_searchText"
                          Label="搜索节点"
                          Variant="Variant.Outlined"
                          Adornment="Adornment.Start"
                          AdornmentIcon="@Icons.Material.Filled.Search"
                          Class="mb-2"
                          Clearable="true"
                          DebounceInterval="300"
                          OnDebounceIntervalElapsed="OnSearchAsync" />

            @if (_filteredNodes is not null && _filteredNodes.Count == 0)
            {
                <MudText Align="Align.Center" Color="Color.Secondary" Class="my-4">
                    未找到匹配的节点
                </MudText>
            }
            else
            {
                <MudTreeView T="string"
                             @bind-SelectedValue="_selectedId"
                             SelectionMode="SelectionMode.SingleSelection">
                    @RenderTreeItems(BuildTreeLevels())
                </MudTreeView>
            }
        </MudContainer>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Close"
                   Variant="Variant.Text"
                   Color="Color.Default">
            取消
        </MudButton>
        <MudButton OnClick="ConfirmSelection"
                   Variant="Variant.Filled"
                   Color="Color.Primary"
                   Disabled="!CanConfirm">
            确认选择
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    /// <summary>扁平节点列表（必须包含 ParentId）。</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<StringNodeMeta> AllNodes { get; set; } = Array.Empty<StringNodeMeta>();

    [Parameter] public StringNodeMeta? CurrentNode { get; set; }
    [Parameter] public StringNodeMeta? CurrentParent { get; set; }
    [Parameter] public bool AllowRootSelection { get; set; } = true;

    private string _searchText = string.Empty;
    private string? _selectedId;
    private List<StringNodeMeta>? _filteredNodes;

    private string SelectedName
        => _selectedId is null
            ? "根节点"
            : AllNodes.FirstOrDefault(n => n.Id == _selectedId)?.Text ?? "根节点";

    private bool CanConfirm => _selectedId != null || AllowRootSelection;

    protected override void OnInitialized()
    {
        _selectedId = CurrentParent?.Id;
    }

    // ============================================================
    // 层级构建：扁平列表 → 顶层列表（按 ParentId 分组）
    // ============================================================

    /// <summary>当前视图下（考虑搜索过滤）的顶层节点。</summary>
    private List<StringNodeMeta> BuildTreeLevels()
    {
        var source = _filteredNodes ?? AllNodes.ToList();
        var ids = source.Select(n => n.Id).ToHashSet();

        // 父节点不在当前视图中的，视为顶层
        return source
            .Where(n => n.ParentId is null || !ids.Contains(n.ParentId))
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.Text)
            .ToList();
    }

    private List<StringNodeMeta> GetChildren(string parentId)
    {
        var source = _filteredNodes ?? AllNodes.ToList();
        return source
            .Where(n => n.ParentId == parentId)
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.Text)
            .ToList();
    }

    // ============================================================
    // 渲染
    // ============================================================

    private RenderFragment RenderTreeItems(List<StringNodeMeta> items) => builder =>
    {
        foreach (var item in items)
        {
            var isDisabled = IsNodeDisabled(item);
            var isSelected = item.Id == _selectedId;

            builder.OpenComponent<MudTreeViewItem<string>>(0);
            builder.AddAttribute(1, "Value", item.Id);
            builder.AddAttribute(2, "Text", item.Text);
            builder.AddAttribute(3, "Icon", GetNodeIcon(item, isSelected));
            builder.AddAttribute(4, "IconColor", GetNodeColor(item, isSelected));
            builder.AddAttribute(5, "Disabled", isDisabled);
            builder.AddAttribute(6, "OnClick",
                // MudTreeViewItem<T>.OnClick 是 EventCallback<MouseEventArgs>，
                // 不能传 EventCallback<string>（运行时类型不匹配导致对话框渲染失败）
                EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(
                    this, _ => OnNodeClick(item.Id)));

            var children = GetChildren(item.Id);
            if (children.Count > 0)
            {
                builder.AddAttribute(7, "ChildContent", RenderTreeItems(children));
            }

            builder.CloseComponent();
        }
    };

    private void OnNodeClick(string id)
    {
        var node = AllNodes.FirstOrDefault(n => n.Id == id);
        if (node is null || IsNodeDisabled(node)) return;

        _selectedId = id;
        StateHasChanged();
    }

    // ============================================================
    // 禁用逻辑：不能选自身或后代
    // ============================================================

    private bool IsNodeDisabled(StringNodeMeta node)
    {
        if (CurrentNode is null) return false;

        var current = node;
        while (current is not null)
        {
            if (current.Id == CurrentNode.Id) return true;

            var parentId = current.ParentId;
            current = parentId is null
                ? null
                : AllNodes.FirstOrDefault(n => n.Id == parentId);
        }

        return false;
    }

    // ============================================================
    // 搜索
    // ============================================================

    private async Task OnSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            _filteredNodes = null;
            return;
        }

        await Task.Delay(100);

        var keyword = _searchText.ToLowerInvariant();
        var matchedIds = AllNodes
            .Where(n => n.Text.ToLowerInvariant().Contains(keyword))
            .Select(n => n.Id)
            .ToHashSet();

        // 保留匹配节点 + 其所有祖先（保持树形结构完整）
        var result = new HashSet<string>();
        foreach (var id in matchedIds)
        {
            var node = AllNodes.FirstOrDefault(n => n.Id == id);
            while (node is not null)
            {
                result.Add(node.Id);
                node = node.ParentId is null
                    ? null
                    : AllNodes.FirstOrDefault(n => n.Id == node.ParentId);
            }
        }

        _filteredNodes = AllNodes.Where(n => result.Contains(n.Id)).ToList();
        StateHasChanged();
    }

    // ============================================================
    // 图标 / 颜色
    // ============================================================

    private string GetNodeIcon(StringNodeMeta node, bool isSelected)
    {
        if (isSelected) return Icons.Material.Filled.CheckCircle;
        if (node.HasChildren) return Icons.Material.Filled.Folder;
        return Icons.Material.Filled.InsertDriveFile;
    }

    private Color GetNodeColor(StringNodeMeta node, bool isSelected)
    {
        if (isSelected) return Color.Primary;
        if (IsNodeDisabled(node)) return Color.Default;
        return Color.Inherit;
    }

    // ============================================================
    // 确认 / 取消
    // ============================================================

    private void ConfirmSelection()
    {
        StringNodeMeta? selected = _selectedId is null
            ? null
            : AllNodes.FirstOrDefault(n => n.Id == _selectedId);

        var result = new StringParentSelectResult
        {
            IsConfirmed = true,
            SelectedParent = selected,
        };

        MudDialog?.Close(DialogResult.Ok(result));
    }

    private void Close()
    {
        MudDialog?.Close(DialogResult.Cancel());
    }
}
```

## 文件 43/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringTreeDialogPageSky.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components
@using TreeGraph.Blazor.Shared.Common
@using TreeGraph.Blazor.Shared.Trees.StringTree.Models

<DialogPageSky Title="@Title" MaxWidth="@MaxWidth">
    <ToolBarContent>
        @if (!string.IsNullOrEmpty(TitleToolBar))
        {
            <MudText Typo="Typo.h6" Class="text-info">@TitleToolBar</MudText>
        }
        @if (ToolBarContent is not null)
        {
            @ToolBarContent
        }
        <MudSpacer/>
    </ToolBarContent>
    <DialogContent>
        <MudContainer MaxWidth="@MaxWidth">
            <StringTreeSky RootId="@RootId"
                           IsSelectDialog="@IsSelectDialog"
                           EditTemplate="@EditTemplate"
                           ActionTemplate="@ActionTemplate" />
        </MudContainer>
    </DialogContent>
</DialogPageSky>

@code {
    [Parameter] public required string Title { get; set; }
    [Parameter] public string? TitleToolBar { get; set; }
    [Parameter] public string? RootId { get; set; }
    [Parameter] public MaxWidth MaxWidth { get; set; } = MaxWidth.Small;
    [Parameter] public bool IsSelectDialog { get; set; }

    [Parameter] public RenderFragment? ToolBarContent { get; set; }
    [Parameter] public RenderFragment<StringNodeMeta>? EditTemplate { get; set; }
    [Parameter] public RenderFragment<StringNodeMeta>? ActionTemplate { get; set; }
}
```

## 文件 44/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringTreeSky.razor

```razor
@* StringTreeSky.razor *@
@namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components
@using TreeGraph.Blazor.Shared.Common
@using TreeGraph.Blazor.Shared.Trees.Dialogs
@using TreeGraph.Blazor.Shared.Trees.StringTree.Models
@using TreeGraph.Blazor.Shared.Trees.StringTree.Services

@inject IStringTreeDataSource DataSource
@inject IStringTreeActionHandler ActionHandler
@inject StringTreeDialogService DialogSvc
@inject MessageService Message
@inject IDialogService DialogService

@if (_loading || _items is null)
{
    <ProgressCircularSky/>
}
else
{
    <MudTreeView T="string"
                 ServerData="@(p => LoadChildrenAsync(p))"
                 Items="@_items"
                 @bind-SelectedValue="SelectedValue"
                 SelectionMode="SelectionMode.ToggleSelection"
                 Dense="@Dense">
        <ItemTemplate>
            <MudTreeViewItem T="string"
                             Value="@context.Value"
                             Text="@context.Text"
                             @bind-Items="context.Children"
                             @bind-Expanded="context.Expanded"
                             CanExpand="@context.Expandable"
                             Icon="@context.Icon"
                             IconColor="@Color.Info"
                             LoadingIconColor="Color.Info"
                             OnClick="@(() => ClickItemAsync(context))">
                <BodyContent Context="node">
                    <MudContainer class="d-flex justify-space-between mx-0 px-0">
                        <MudText>
                            <MudHighlighter Text="@context.Text"
                                            HighlightedText="@HighlightedText"
                                            Class="mud-theme-primary"/>
                        </MudText>

                        @if (!IsSelectDialog)
                        {
                            <MudIconButton Icon="@Icons.Material.Filled.MoreHoriz"
                                           Size="Size.Small"
                                           Color="Color.Primary"
                                           OnClick="@(() => ShowNodeActionsAsync(context))"/>
                        }
                    </MudContainer>
                </BodyContent>
            </MudTreeViewItem>
        </ItemTemplate>
    </MudTreeView>
}
```

## 文件 45/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringTreeSky.razor.Action.cs

```csharp
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components;

public partial class StringTreeSky
{
    // ============================================================
    // 节点操作入口
    // ============================================================

    private async Task ShowNodeActionsAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;

        var meta = GetMeta(node.Value);
        if (meta is null) return;

        var isBoot = node.Value == RootId
            || (_items is { Count: 1 } && _items[0].Value == node.Value);

        var parentMeta = string.IsNullOrEmpty(meta.ParentId)
            ? null
            : GetMeta(meta.ParentId);

        var hasChildren = node.Children is { Count: > 0 } || meta.HasChildren;

        var result = await DialogSvc.ShowActionsDialogAsync(
            meta, parentMeta, isBoot, hasChildren);
        if (result is null) return;

        await ExecuteNodeActionAsync(result.Action, node);
    }

    public async Task ExecuteNodeActionAsync(
        StringNodeAction action, ITreeItemData<string> node)
    {
        switch (action)
        {
            case StringNodeAction.View:
                await HandleViewAsync(node);
                break;
            case StringNodeAction.AddChild:
                await HandleAddChildAsync(node);
                break;
            case StringNodeAction.Edit:
                await HandleEditAsync(node);
                break;
            case StringNodeAction.Delete:
                await HandleDeleteAsync(node);
                break;
            case StringNodeAction.Move:
                await HandleMoveAsync(node);
                break;
            case StringNodeAction.Sort:
                await HandleSortAsync(node);
                break;
        }
    }

    // ============================================================
    // 查看详情
    // ============================================================

    private async Task HandleViewAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;
        var meta = GetMeta(node.Value);
        if (meta is null) return;

        var parentMeta = string.IsNullOrEmpty(meta.ParentId)
            ? null
            : GetMeta(meta.ParentId);

        await DialogSvc.ShowViewDialogAsync(meta, parentMeta);
    }

    // ============================================================
    // 创建子项
    // ============================================================

    private async Task HandleAddChildAsync(ITreeItemData<string> parent)
    {
        if (string.IsNullOrEmpty(parent.Value)) return;
        var parentMeta = GetMeta(parent.Value);
        if (parentMeta is null) return;

        var formModel = await DialogSvc.ShowCreateDialogAsync(
            parentMeta, EditTemplate);
        if (formModel is null) return;
        if (_cts.IsCancellationRequested) return;

        try
        {
            var created = await ActionHandler.CreateChildAsync(
                parent.Value, formModel, _cts.Token);
            if (created is null)
            {
                Message.Warning("创建失败");
                return;
            }

            _metaCache[created.Id] = created;

            parent.Expanded = true;
            await RefreshNodeChildrenAsync(parent, _cts.Token);
            Message.Success("创建成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("创建失败", e.Message);
        }
    }

    // ============================================================
    // 编辑
    // ============================================================

    private async Task HandleEditAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;
        var meta = GetMeta(node.Value);
        if (meta is null) return;

        var formModel = await DialogSvc.ShowEditDialogAsync(meta, EditTemplate);
        if (formModel is null) return;
        if (_cts.IsCancellationRequested) return;

        try
        {
            var updated = await ActionHandler.UpdateNodeAsync(formModel, _cts.Token);
            if (updated is null)
            {
                Message.Warning("更新失败");
                return;
            }

            _metaCache[updated.Id] = updated;
            node.Text = updated.Text;   // 立即反映到 UI
            Message.Success("更新成功");
            StateHasChanged();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("更新失败", e.Message);
        }
    }

    // ============================================================
    // 删除
    // ============================================================

    private async Task HandleDeleteAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;
        var meta = GetMeta(node.Value);
        if (meta is null) return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除节点【{meta.Text}】及其所有子节点吗？",
            yesText: "删除",
            cancelText: "取消");

        if (confirmed != true) return;
        if (_cts.IsCancellationRequested) return;

        try
        {
            var ok = await ActionHandler.DeleteNodeAsync(node.Value, _cts.Token);
            if (!ok)
            {
                Message.Warning("删除失败，节点不存在");
                return;
            }

            _metaCache.Remove(node.Value);

            if (_items is not null)
                RemoveNodeFromParent(_items, node.Value);

            Message.Success("删除成功");
            StateHasChanged();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("删除失败", e.Message);
        }
    }

    // ============================================================
    // 移动
    // ============================================================

    private async Task HandleMoveAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;
        var meta = GetMeta(node.Value);
        if (meta is null) return;

        // 加载所有节点（可能触发懒加载）
        var allNodes = await EnsureAllNodesLoadedAsync(_cts.Token);
        if (allNodes.Count == 0) return;

        var parentMeta = string.IsNullOrEmpty(meta.ParentId)
            ? null
            : GetMeta(meta.ParentId);

        var result = await DialogSvc.ShowParentSelectDialogAsync(
            allNodes, currentNode: meta, currentParent: parentMeta);
        if (result is null || !result.IsConfirmed) return;

        var newParentId = result.SelectedParent?.Id;
        if (newParentId == meta.ParentId)
        {
            Message.Info("父节点未变化");
            return;
        }

        var confirmed = await Message.BoolBoxAsync(
            $"确定将【{meta.Text}】移动到" +
            $"【{result.SelectedParent?.Text ?? "根节点"}】下吗？");
        if (!confirmed) return;

        try
        {
            var ok = await ActionHandler.MoveNodeAsync(
                node.Value, newParentId, _cts.Token);
            if (!ok)
            {
                Message.Warning("移动失败，可能构成环或节点不存在");
                return;
            }

            // API 成功后再同步内存
            meta.ParentId = newParentId;
            await RefreshAsync(_cts.Token);
            Message.Success("移动成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("移动失败", e.Message);
        }
    }

    // ============================================================
    // 排序
    // ============================================================

    private async Task HandleSortAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;

        var childrenMeta = node.Children?
            .OfType<TreeItemData<string>>()
            .Select(c => string.IsNullOrEmpty(c.Value) ? null : GetMeta(c.Value))
            .Where(m => m is not null)
            .Cast<StringNodeMeta>()
            .ToList() ?? new List<StringNodeMeta>();

        if (childrenMeta.Count < 2)
        {
            Message.Info("至少需要 2 个子节点才能排序");
            return;
        }

        var orderedIds = await DialogSvc.ShowSortDialogAsync(childrenMeta);
        if (orderedIds is null) return;

        try
        {
            var ok = await ActionHandler.SortChildrenAsync(
                node.Value, orderedIds, _cts.Token);
            if (!ok) return;

            await RefreshNodeChildrenAsync(node, _cts.Token);
            Message.Success("排序成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("排序失败", e.Message);
        }
    }
}
```

## 文件 46/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringTreeSky.razor.cs

```csharp
using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components;

public partial class StringTreeSky : IDisposable
{
    // ============================================================
    // 内部状态
    // ============================================================

    private List<TreeItemData<string>>? _items;
    private string? _lastClickNodeId;
    private string? _pendingDeepClickNodeId;
    private bool _loading = true;

    /// <summary>
    /// string ID → 元数据 缓存。
    ///
    /// ★ T=string 无法承载显示信息（Text/Icon），所有元数据按 ID 缓存。
    ///   每次 LoadChildren / GetById 返回时更新。
    /// </summary>
    private readonly Dictionary<string, StringNodeMeta> _metaCache = new();

    private readonly CancellationTokenSource _cts = new();

    // ============================================================
    // 参数
    // ============================================================

    /// <summary>根节点 ID。null 时用 DataSource 返回的所有根。</summary>
    [Parameter] public string? RootId { get; set; }

    /// <summary>要展开并选中的目标节点 ID（用于 URL 直达深层节点）。</summary>
    [Parameter] public string? ClickNodeId { get; set; }

    /// <summary>搜索高亮文本。</summary>
    [Parameter] public string? HighlightedText { get; set; }

    /// <summary>紧凑模式。</summary>
    [Parameter] public bool Dense { get; set; } = true;

    /// <summary>是否为"选择节点"对话框场景（隐藏操作按钮）。</summary>
    [Parameter] public bool IsSelectDialog { get; set; }

    /// <summary>当前选中节点 ID（双向绑定）。</summary>
    [Parameter] public string? SelectedValue { get; set; }

    /// <summary>选中变化回调（双向绑定另一半）。</summary>
    [Parameter] public EventCallback<string?> SelectedValueChanged { get; set; }

    /// <summary>节点点击回调（参数为完整 ITreeItemData，调用方可直接取 Text/Icon）。</summary>
    [Parameter] public EventCallback<ITreeItemData<string>?> OnClickItemText { get; set; }

    /// <summary>编辑模板（创建 / 编辑节点时弹表单）。</summary>
    [Parameter] public RenderFragment<StringNodeMeta>? EditTemplate { get; set; }

    /// <summary>操作模板（节点右侧自定义操作区，可选）。</summary>
    [Parameter] public RenderFragment<StringNodeMeta>? ActionTemplate { get; set; }

    // ============================================================
    // 生命周期
    // ============================================================

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _items = await LoadInitialDataAsync(_cts.Token);
            await SetSelectedAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 组件卸载 / 导航取消，静默忽略
        }
        catch (Exception e)
        {
            Message.Details("数据加载失败。", e.Message);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_cts.IsCancellationRequested) return;

        if (!string.IsNullOrEmpty(ClickNodeId) && ClickNodeId != _lastClickNodeId)
        {
            _lastClickNodeId = ClickNodeId;
            try
            {
                await ExpandToNodeAsync(ClickNodeId, ct: _cts.Token);
            }
            catch (OperationCanceledException) { }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 深层书签直达：等 MudTreeView 挂载完成后再沿路径展开并选中
        if (firstRender && _pendingDeepClickNodeId is { } targetId)
        {
            _pendingDeepClickNodeId = null;
            try
            {
                await ExpandToNodeAsync(targetId, clearSelection: false, ct: _cts.Token);
            }
            catch (OperationCanceledException) { }
        }
    }

    // ============================================================
    // IDisposable
    // ============================================================

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
```

## 文件 47/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringTreeSky.razor.Loading.cs

```csharp
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components;

public partial class StringTreeSky
{
    // ============================================================
    // 初始数据加载
    // ============================================================

    private async Task<List<TreeItemData<string>>> LoadInitialDataAsync(
        CancellationToken ct = default)
    {
        try
        {
            _loading = true;

            var roots = await DataSource.GetRootsAsync(ct);
            ct.ThrowIfCancellationRequested();

            _loading = false;

            return roots.Select(ToTreeItemData).ToList();
        }
        catch (OperationCanceledException)
        {
            _loading = false;
            throw;
        }
        catch (Exception e)
        {
            _loading = false;
            Message.Details("加载初始数据失败。", e.Message);
            return [];
        }
    }

    // ============================================================
    // ServerData 回调（MudTreeView 展开未预置节点时调用）
    // ============================================================

    private async Task<IReadOnlyCollection<TreeItemData<string>>> LoadChildrenAsync(
        string? parentValue)
    {
        try
        {
            var ct = _cts.Token;

            // 防御：MudTreeView 理论上不传 null，但保留兜底
            if (string.IsNullOrEmpty(parentValue))
            {
                var roots = await DataSource.GetRootsAsync(ct);
                return roots.Select(ToTreeItemData).ToList();
            }

            var children = await DataSource.GetChildrenAsync(parentValue, ct);
            return children.Select(ToTreeItemData).ToList();
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception e)
        {
            Message.Details("加载子节点失败。", e.Message);
            return [];
        }
    }

    // ============================================================
    // DTO 转换（同时写入 meta 缓存）
    // ============================================================

    private TreeItemData<string> ToTreeItemData(StringNodeMeta meta)
    {
        // ★ 关键：Value 是 GUID，Text/Icon 挂到 TreeItemData 上（不靠 _metaCache 渲染）
        _metaCache[meta.Id] = meta;

        return new TreeItemData<string>
        {
            Value = meta.Id,
            Text = meta.Text,
            Icon = meta.Icon,
            Expandable = meta.HasChildren,
            Expanded = false,
            Selected = false,
        };
    }

    // ============================================================
    // 刷新
    // ============================================================

    /// <summary>重新加载根节点（会清空选中）。</summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        _items = await LoadInitialDataAsync(ct);

        SelectedValue = null;
        _ = SelectedValueChanged.InvokeAsync(null);
        StateHasChanged();
    }

    /// <summary>刷新指定节点的子节点。</summary>
    private async Task RefreshNodeChildrenAsync(
        ITreeItemData<string> node,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(node.Value)) return;

        SelectedValue = null;
        _ = SelectedValueChanged.InvokeAsync(null);
        StateHasChanged();

        var children = await DataSource.GetChildrenAsync(node.Value, ct);
        ct.ThrowIfCancellationRequested();

        node.Children = children
            .Select(ToTreeItemData)
            .ToList<ITreeItemData<string>>();

        node.Expanded = true;
        SelectedValue = node.Value;
        _ = SelectedValueChanged.InvokeAsync(node.Value);

        StateHasChanged();
    }
}
```

## 文件 48/66 TreeGraph.Blazor.Shared/Trees/StringTree/Components/StringTreeSky.razor.Node.cs

```csharp
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components;

public partial class StringTreeSky
{
    // ============================================================
    // 元数据访问
    // ============================================================

    private StringNodeMeta? GetMeta(string? id)
        => string.IsNullOrEmpty(id) ? null : _metaCache.GetValueOrDefault(id);

    // ============================================================
    // 节点点击
    // ============================================================

    private async Task ClickItemAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;

        SelectedValue = node.Value;
        await SelectedValueChanged.InvokeAsync(node.Value);

        // 统一走回调（调用方根据 IsSelectDialog 决定行为）
        await OnClickItemText.InvokeAsync(node);
    }

    // ============================================================
    // 初始选中（含深层节点延迟处理）
    // ============================================================

    private Task SetSelectedAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(ClickNodeId)) return Task.CompletedTask;
        if (_items is not { Count: > 0 }) return Task.CompletedTask;

        var found = FindNodeById(_items, ClickNodeId);
        if (found is not null)
        {
            SelectedValue = ClickNodeId;
            _ = SelectedValueChanged.InvokeAsync(ClickNodeId);
            _lastClickNodeId = ClickNodeId;
            return Task.CompletedTask;
        }

        // 深层节点：延后到首帧渲染后处理
        _pendingDeepClickNodeId = ClickNodeId;
        _lastClickNodeId = ClickNodeId;
        return Task.CompletedTask;
    }

    // ============================================================
    // 沿路径展开到目标节点
    // ============================================================

    private async Task ExpandToNodeAsync(
        string targetId, bool clearSelection = true,
        CancellationToken ct = default)
    {
        // 1. 先试已加载的 _items
        var path = _items is not null
            ? GetPathToNode(_items, targetId)
            : null;

        // 2. 未找到 → 走 API 祖先路径
        if (path is null)
        {
            path = await DataSource.GetAncestorPathAsync(targetId, ct);
        }

        ct.ThrowIfCancellationRequested();
        if (path is not { Count: > 0 }) return;

        // 3. 清空旧选中（首次挂载恢复时不清，避免 MudTreeView 首帧覆盖）
        if (clearSelection)
        {
            SelectedValue = null;
            _ = SelectedValueChanged.InvokeAsync(null);
            StateHasChanged();
        }

        // 4. 沿路径逐层展开
        await ExpandPathAsync(path, ct);

        StateHasChanged();
    }

    private async Task ExpandPathAsync(List<string> path, CancellationToken ct)
    {
        var currentItems = _items;
        if (currentItems is null) return;

        for (int i = 0; i < path.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var nodeId = path[i];
            var item = currentItems.FirstOrDefault(x => x.Value == nodeId);
            if (item is null) break;

            // 目标节点：只选中，不展开
            if (i == path.Count - 1)
            {
                SelectedValue = nodeId;
                _ = SelectedValueChanged.InvokeAsync(nodeId);
                break;
            }

            // 中间节点：展开 + 加载子节点
            item.Expanded = true;

            if (item.Children is not { Count: > 0 } && item.Expandable)
            {
                var children = await DataSource.GetChildrenAsync(nodeId, ct);
                ct.ThrowIfCancellationRequested();

                item.Children = children
                    .Select(ToTreeItemData)
                    .ToList<ITreeItemData<string>>();
            }

            currentItems = item.Children?
                .OfType<TreeItemData<string>>()
                .ToList() ?? new List<TreeItemData<string>>();
        }
    }

    // ============================================================
    // 查找（显式栈迭代）
    // ============================================================

    private TreeItemData<string>? FindNodeById(
        IEnumerable<TreeItemData<string>> items, string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var stack = new Stack<TreeItemData<string>>(items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.Value == id) return current;

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
            {
                if (child is TreeItemData<string> tc) stack.Push(tc);
            }
        }

        return null;
    }

    /// <summary>BFS + 父指针回溯，返回从根到目标的 ID 路径。</summary>
    private static List<string>? GetPathToNode(
        IEnumerable<TreeItemData<string>> items, string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;

        var parentMap = new Dictionary<string, string?>();
        var queue = new Queue<TreeItemData<string>>();

        foreach (var root in items)
        {
            if (string.IsNullOrEmpty(root.Value)) continue;
            parentMap.TryAdd(root.Value, null);
            queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (string.IsNullOrEmpty(current.Value)) continue;

            if (current.Value == targetId)
            {
                var path = new List<string>();
                string? cursor = targetId;
                while (cursor is not null)
                {
                    path.Add(cursor);
                    cursor = parentMap.TryGetValue(cursor, out var p) ? p : null;
                }
                path.Reverse();
                return path;
            }

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
            {
                if (child is not TreeItemData<string> { Value: not null } tc) continue;
                if (parentMap.ContainsKey(tc.Value)) continue;   // 防环

                parentMap[tc.Value] = current.Value;
                queue.Enqueue(tc);
            }
        }

        return null;
    }

    // ============================================================
    // 移除节点（迭代 + 深层回写）
    // ============================================================

    public bool RemoveNodeFromParent(List<TreeItemData<string>> items, string id)
    {
        if (string.IsNullOrEmpty(id) || items.Count == 0) return false;

        var stack = new Stack<(System.Collections.IList Level, ITreeItemData<string>? Owner)>();
        stack.Push((items, null));

        while (stack.Count > 0)
        {
            var (level, owner) = stack.Pop();

            for (int i = 0; i < level.Count; i++)
            {
                var node = (ITreeItemData<string>)level[i]!;
                if (node.Value == id)
                {
                    level.RemoveAt(i);

                    if (owner is not null)
                    {
                        owner.Children = (List<ITreeItemData<string>>)level;
                    }
                    return true;
                }
            }

            for (int i = level.Count - 1; i >= 0; i--)
            {
                var node = (ITreeItemData<string>)level[i]!;
                if (node.Children is not { Count: > 0 }) continue;

                if (node.Children is not System.Collections.IList childLevel)
                {
                    var materialized = node.Children
                        .OfType<TreeItemData<string>>()
                        .ToList<ITreeItemData<string>>();
                    node.Children = materialized;
                    childLevel = materialized;
                }

                stack.Push((childLevel, node));
            }
        }

        return false;
    }

    // ============================================================
    // 全量节点
    // ============================================================

    /// <summary>只读：返回当前已加载的全部节点 meta，不触发懒加载。</summary>
    public List<StringNodeMeta> GetAllLoadedNodes()
    {
        var result = new List<StringNodeMeta>();
        if (_items is null) return result;

        var stack = new Stack<TreeItemData<string>>(_items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (!string.IsNullOrEmpty(current.Value))
            {
                var meta = GetMeta(current.Value);
                if (meta is not null) result.Add(meta);
            }

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
            {
                if (child is TreeItemData<string> tc) stack.Push(tc);
            }
        }

        return result;
    }

    /// <summary>确保全部节点已加载后返回扁平 meta 列表（有副作用）。</summary>
    public async Task<List<StringNodeMeta>> EnsureAllNodesLoadedAsync(
        CancellationToken ct = default)
    {
        var result = new List<StringNodeMeta>();
        if (_items is null) return result;

        var queue = new Queue<TreeItemData<string>>(_items);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = queue.Dequeue();

            if (!string.IsNullOrEmpty(current.Value))
            {
                var meta = GetMeta(current.Value);
                if (meta is not null) result.Add(meta);

                // 未展开但有子节点 → 懒加载
                if (current.Children?.Any() != true && meta?.HasChildren == true)
                {
                    var children = await DataSource.GetChildrenAsync(current.Value, ct);
                    ct.ThrowIfCancellationRequested();

                    current.Children = children
                        .Select(ToTreeItemData)
                        .ToList<ITreeItemData<string>>();
                }
            }

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
            {
                if (child is TreeItemData<string> tc) queue.Enqueue(tc);
            }
        }

        return result;
    }
}
```

## 文件 49/66 TreeGraph.Blazor.Shared/Trees/StringTree/Contracts/StringNodeActionResult.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Contracts;

/// <summary>节点操作结果（操作 + 目标节点）。</summary>
public class StringNodeActionResult
{
    public StringNodeAction Action { get; set; }
    public required StringNodeMeta Node { get; set; }
}
```

## 文件 50/66 TreeGraph.Blazor.Shared/Trees/StringTree/Contracts/StringNodeTemplate.cs

```csharp
using Microsoft.AspNetCore.Components;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Contracts;

/// <summary>
/// 节点模板容器：把"目标节点 + 编辑 UI"绑定到一起。
/// 与 Trees.Contracts.NodeTemplate&lt;TItem&gt; 对应。
/// </summary>
public class StringNodeTemplate
{
    /// <summary>目标节点元数据（引用原对象）。</summary>
    public required StringNodeMeta Node { get; set; }

    /// <summary>节点操作区模板（可选）。</summary>
    public RenderFragment<StringNodeMeta>? ActionTemplate { get; set; }

    /// <summary>节点编辑模板（可选）。</summary>
    public RenderFragment<StringNodeMeta>? EditTemplate { get; set; }
}
```

## 文件 51/66 TreeGraph.Blazor.Shared/Trees/StringTree/Contracts/StringParentSelectResult.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Contracts;

/// <summary>父节点选择结果。</summary>
public class StringParentSelectResult
{
    /// <summary>是否确认选择。</summary>
    public bool IsConfirmed { get; set; }

    /// <summary>选中的父节点（null 表示根节点）。</summary>
    public StringNodeMeta? SelectedParent { get; set; }

    /// <summary>选中节点的路径 ID 列表。</summary>
    public List<string> SelectedPath { get; set; } = new();
}
```

## 文件 52/66 TreeGraph.Blazor.Shared/Trees/StringTree/Extensions/StringTreeServiceCollectionExtensions.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Extensions;

public static class StringTreeServiceCollectionExtensions
{
    /// <summary>
    /// 注册 StringTreeSky 组件所需服务：
    ///   - IStringTreeActionHandler（默认 Noop，宿主可用 AddScoped 覆盖）
    ///   - StringTreeDialogService
    ///
    /// ★ IStringTreeDataSource 由宿主按业务注册（不在此处 AddScoped）。
    /// ★ 若宿主已 AddMudServices，本方法无额外前置条件。
    /// </summary>
    public static IServiceCollection AddStringTreeSky(this IServiceCollection services)
    {
        // TryAdd：宿主若已注册自定义 Handler，不会被覆盖
        services.TryAddScoped<IStringTreeActionHandler, NoopStringTreeActionHandler>();

        services.AddScoped<StringTreeDialogService>();

        return services;
    }
}
```

## 文件 53/66 TreeGraph.Blazor.Shared/Trees/StringTree/Models/StringNodeAction.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.StringTree.Models;

/// <summary>
/// StringTreeSky 的节点操作类型。
/// 与 Trees.Contracts.NodeAction 语义一致，独立定义便于未来分叉。
/// </summary>
public enum StringNodeAction
{
    /// <summary>查看详情</summary>
    View,

    /// <summary>创建子项</summary>
    AddChild,

    /// <summary>编辑节点</summary>
    Edit,

    /// <summary>删除节点（含后代）</summary>
    Delete,

    /// <summary>移动节点到其它父节点</summary>
    Move,

    /// <summary>重排子节点顺序</summary>
    Sort,
}
```

## 文件 54/66 TreeGraph.Blazor.Shared/Trees/StringTree/Models/StringNodeMeta.cs

```csharp
namespace TreeGraph.Blazor.Shared.Trees.StringTree.Models;

/// <summary>
/// T=string 树节点的元数据。
///
/// ★ Value 是 GUID（不可读标识），Text 是显示名——两者分离。
///   MudTreeView&lt;string&gt; 承载 Value，Text / Icon 通过 TreeItemData 携带。
/// </summary>
public class StringNodeMeta
{
    /// <summary>节点唯一 ID（GUID 字符串，即 MudTreeView&lt;string&gt; 的 Value）</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>父节点 ID（null = 根节点）</summary>
    public string? ParentId { get; set; }

    /// <summary>显示文本</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>描述（详情对话框展示）</summary>
    public string? Description { get; set; }

    /// <summary>副标题（可选）</summary>
    public string? Subtitle { get; set; }

    /// <summary>MudBlazor 图标常量（可选）</summary>
    public string? Icon { get; set; }

    /// <summary>是否实际有子节点（决定展开箭头）</summary>
    public bool HasChildren { get; set; }

    /// <summary>
    /// 是否允许添加子节点。
    /// false 时：即使有子节点也不允许"创建子项"（如系统分类不可扩展）。
    /// </summary>
    public bool CanHaveChildren { get; set; } = true;

    /// <summary>排序序号（越小越靠前）</summary>
    public int SortOrder { get; set; }

    /// <summary>扩展数据（业务自定义字段）</summary>
    public Dictionary<string, object?>? ExtraData { get; set; }

    /// <summary>浅拷贝（用于编辑模板 / 对话框传值不污染原对象）。</summary>
    public StringNodeMeta Clone() => new()
    {
        Id = Id,
        ParentId = ParentId,
        Text = Text,
        Description = Description,
        Subtitle = Subtitle,
        Icon = Icon,
        HasChildren = HasChildren,
        CanHaveChildren = CanHaveChildren,
        SortOrder = SortOrder,
        ExtraData = ExtraData is null
            ? null
            : new Dictionary<string, object?>(ExtraData),
    };
}
```

## 文件 55/66 TreeGraph.Blazor.Shared/Trees/StringTree/Services/IStringTreeActionHandler.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// StringTreeSky 的写操作接口。
///
/// 与 <see cref="IStringTreeDataSource"/> 的关系：
///   - 读操作 → IStringTreeDataSource
///   - 写操作 → 本接口
///
/// 默认实现 <see cref="NoopStringTreeActionHandler"/> 抛 NotSupportedException，
/// 纯展示场景无需实现；管理场景请注册自定义 Handler。
/// </summary>
public interface IStringTreeActionHandler
{
    /// <summary>
    /// 创建子节点。
    /// 入参：父节点 ID + 新节点元数据（Id 可为空，由实现方生成）。
    /// 返回：创建后的节点（含新 Id）；失败返回 null。
    /// </summary>
    Task<StringNodeMeta?> CreateChildAsync(
        string parentId, StringNodeMeta newChild, CancellationToken ct = default);

    /// <summary>更新节点信息（不含 ParentId，移动请用 <see cref="MoveNodeAsync"/>）。</summary>
    Task<StringNodeMeta?> UpdateNodeAsync(
        StringNodeMeta node, CancellationToken ct = default);

    /// <summary>删除节点及其全部后代。返回是否删除成功。</summary>
    Task<bool> DeleteNodeAsync(
        string id, CancellationToken ct = default);

    /// <summary>移动节点到新父（null = 移至根）。返回是否成功。</summary>
    Task<bool> MoveNodeAsync(
        string id, string? newParentId, CancellationToken ct = default);

    /// <summary>
    /// 重排子节点顺序。
    ///
    /// ★ 语义：orderedChildIds 顺序即最终顺序，按列表位置重编号。
    ///   不要按每个节点的 SortOrder 排序（会撤销拖拽结果）。
    /// </summary>
    Task<bool> SortChildrenAsync(
        string parentId, IReadOnlyList<string> orderedChildIds,
        CancellationToken ct = default);
}
```

## 文件 56/66 TreeGraph.Blazor.Shared/Trees/StringTree/Services/IStringTreeDataSource.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// T=string 树的数据源接口（只读）。
///
/// 与 <see cref="Trees.Services.ITreeClientService{TTree}"/> 的区别：
///   - 泛型被固定为 string（节点 ID）
///   - 元数据通过 <see cref="StringNodeMeta"/> 传递
/// </summary>
public interface IStringTreeDataSource
{
    /// <summary>获取根节点列表（按 SortOrder 排序）。</summary>
    Task<IReadOnlyList<StringNodeMeta>> GetRootsAsync(
        CancellationToken ct = default);

    /// <summary>获取指定父节点的子节点（懒加载）。</summary>
    Task<IReadOnlyList<StringNodeMeta>> GetChildrenAsync(
        string parentId, CancellationToken ct = default);

    /// <summary>按 ID 获取单个节点（刷新 / 详情用）。不存在返回 null。</summary>
    Task<StringNodeMeta?> GetByIdAsync(
        string id, CancellationToken ct = default);

    // ★ 批次 3 新增：返回从根到目标的 ID 路径（含目标自身，根在前）
    //    语义对齐 Trees.Services.ITreeClientService<TTree>.GetAncestorPathFromApiAsync
    Task<List<string>?> GetAncestorPathAsync(string id, CancellationToken ct = default);
}
```

## 文件 57/66 TreeGraph.Blazor.Shared/Trees/StringTree/Services/NoopStringTreeActionHandler.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// 默认实现：所有写操作抛 NotSupportedException。
///
/// 用途：
///   - 纯展示场景无需注册 Handler 即可使用 StringTreeSky（只读）
///   - 管理场景请注册自定义 Handler 覆盖此默认
/// </summary>
public class NoopStringTreeActionHandler : IStringTreeActionHandler
{
    public Task<StringNodeMeta?> CreateChildAsync(
        string parentId, StringNodeMeta newChild, CancellationToken ct = default)
        => throw NotSupported(nameof(CreateChildAsync));

    public Task<StringNodeMeta?> UpdateNodeAsync(
        StringNodeMeta node, CancellationToken ct = default)
        => throw NotSupported(nameof(UpdateNodeAsync));

    public Task<bool> DeleteNodeAsync(
        string id, CancellationToken ct = default)
        => throw NotSupported(nameof(DeleteNodeAsync));

    public Task<bool> MoveNodeAsync(
        string id, string? newParentId, CancellationToken ct = default)
        => throw NotSupported(nameof(MoveNodeAsync));

    public Task<bool> SortChildrenAsync(
        string parentId, IReadOnlyList<string> orderedChildIds,
        CancellationToken ct = default)
        => throw NotSupported(nameof(SortChildrenAsync));

    private static NotSupportedException NotSupported(string method)
        => new($"StringTreeSky 的 {method} 未注册 ActionHandler。" +
               $"请在 DI 中注册 IStringTreeActionHandler 实现。");
}
```

## 文件 58/66 TreeGraph.Blazor.Shared/Trees/StringTree/Services/StringTreeDialogService.cs

```csharp
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;
using TreeGraph.Blazor.Shared.Trees.Dialogs;
using TreeGraph.Blazor.Shared.Trees.StringTree.Components;
using TreeGraph.Blazor.Shared.Trees.StringTree.Contracts;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// StringTreeSky 对话框服务。
///
/// 对话框选项统一走 <see cref="DialogConfig"/> 的 ToDialogOptions()；
/// 所有对话框返回弱类型 result，由本服务做类型校验后向上传递。
/// </summary>
public class StringTreeDialogService(
    IDialogService dialogService)
{
    // ============================================================
    // 操作选择
    // ============================================================

    public async Task<StringNodeActionResult?> ShowActionsDialogAsync(
        StringNodeMeta node,
        StringNodeMeta? parentNode,
        bool isBoot = false,
        bool hasChildren = false,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "Node", node },
            { "ParentNode", parentNode },
            { "IsBoot", isBoot },
            { "HasChildren", hasChildren },
        };

        var options = (config ?? new DialogConfig { MaxWidth = MaxWidth.Small })
            .ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringNodeActionsDialog>(
            "节点操作", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not StringNodeActionResult ar)
            return null;

        return ar;
    }

    // ============================================================
    // 查看详情
    // ============================================================

    public async Task<bool> ShowViewDialogAsync(
        StringNodeMeta node,
        StringNodeMeta? parentNode = null,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "Node", node },
            { "ParentNode", parentNode },
        };

        var options = (config ?? new DialogConfig { MaxWidth = MaxWidth.Small })
            .ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringNodeViewDialog>(
            "节点详情", parameters, options);

        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    // ============================================================
    // 创建 / 编辑
    // ============================================================

    /// <summary>创建子节点：入参父节点；返回新建的元数据（ParentId 已填）。</summary>
    public async Task<StringNodeMeta?> ShowCreateDialogAsync(
        StringNodeMeta parent,
        RenderFragment<StringNodeMeta>? editTemplate = null,
        DialogConfig? config = null)
    {
        var template = new StringNodeTemplate
        {
            Node = new StringNodeMeta
            {
                ParentId = parent.Id,
                CanHaveChildren = true,
            },
            EditTemplate = editTemplate,
        };

        return await ShowEditDialogAsync(template, parent, isCreate: true, config);
    }

    /// <summary>编辑节点：入参目标节点；返回修改后的元数据。</summary>
    public async Task<StringNodeMeta?> ShowEditDialogAsync(
        StringNodeMeta node,
        RenderFragment<StringNodeMeta>? editTemplate = null,
        DialogConfig? config = null)
    {
        var template = new StringNodeTemplate
        {
            Node = node,
            EditTemplate = editTemplate,
        };

        return await ShowEditDialogAsync(template, parentNode: null, isCreate: false, config);
    }

    private async Task<StringNodeMeta?> ShowEditDialogAsync(
        StringNodeTemplate template,
        StringNodeMeta? parentNode,
        bool isCreate,
        DialogConfig? config)
    {
        var parameters = new DialogParameters
        {
            { "Template", template },
            { "ParentNode", parentNode },
            { "IsCreate", isCreate },
        };

        var options = (config ?? new DialogConfig { MaxWidth = MaxWidth.Small })
            .ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringNodeEditDialog>(
            isCreate ? "创建节点" : "编辑节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not StringNodeMeta meta)
            return null;

        return meta;
    }

    // ============================================================
    // 排序
    // ============================================================

    /// <summary>
    /// 排序对话框。入参为有序子节点列表，返回重排后的 ID 列表（顺序即新顺序）。
    /// </summary>
    public async Task<List<string>?> ShowSortDialogAsync(
        IReadOnlyList<StringNodeMeta> children,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "Children", children },
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.Small,
            CloseButton = false,
            BackdropClick = false,
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringNodeSortDialog>(
            "拖拽排序", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not List<string> sortResult)
            return null;

        return sortResult;
    }

    // ============================================================
    // 父节点选择
    // ============================================================

    /// <summary>
    /// 从扁平节点列表中选择父节点。
    /// 组件内部按 ParentId 构建树形视图，无需 Children 属性。
    /// </summary>
    public async Task<StringParentSelectResult?> ShowParentSelectDialogAsync(
        IReadOnlyList<StringNodeMeta> allNodes,
        StringNodeMeta? currentNode = null,
        StringNodeMeta? currentParent = null,
        bool allowRoot = true,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "AllNodes", allNodes },
            { "CurrentNode", currentNode },
            { "CurrentParent", currentParent },
            { "AllowRootSelection", allowRoot },
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.Medium,
            CloseButton = true,
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringParentSelectDialog>(
            "选择父节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not StringParentSelectResult sr)
            return null;

        return sr;
    }
}
```

## 文件 59/66 TreeGraph.Blazor.Shared/Trees/TreeDialogPageSky.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>, new()

@inject ITreeClientService<TItem> ClientService

<DialogPageSky Title="@Title" MaxWidth="@MaxWidth">
    <ToolBarContent>
        @if (!string.IsNullOrEmpty(TitleToolBar))
        {
            <MudText Typo="Typo.h6" Class="text-info">@TitleToolBar</MudText>
        }

        @if (ToolBarLeftContent != null)
        {
            @ToolBarLeftContent
        }

        <MudSpacer/>

        @if (ToolBarRightContent != null)
        {
            @ToolBarRightContent
        }

        @if (ShowDialogFunc != null)
        {
            <MudCheckBox @bind-Value="@ClientService.NewPageShow"
                         Label="新页面打开"
                         Color="Color.Primary"
                         Size="Size.Small"
                         UncheckedColor="Color.Default"/>
        }
    </ToolBarContent>
    <DialogContent>
        <MudContainer MaxWidth="@MaxWidth">
            <TreeSky TItem="@TItem"
                     RootId="@RootId"
                     ActionTemplate="@ActionTemplate"
                     EditTemplate="@EditTemplate"
                     OnClickItemText="@HandleClickItemText"
                     ShowDialogFunc="@ShowDialogFunc"/>
        </MudContainer>
    </DialogContent>
</DialogPageSky>

@code {
    [Parameter] public string Title { get; set; } = typeof(TItem).Name;
    [Parameter] public string? TitleToolBar { get; set; }
    [Parameter] public string? RootId { get; set; }
    [Parameter] public MaxWidth MaxWidth { get; set; } = MaxWidth.Small;
    [Parameter] public Func<ITreeItemData<TItem>, Task>? ShowDialogFunc { get; set; }

    [Parameter] public RenderFragment? TopBarRightContent { get; set; }
    [Parameter] public RenderFragment? ToolBarLeftContent { get; set; }
    [Parameter] public RenderFragment? ToolBarRightContent { get; set; }

    [Parameter] public RenderFragment<TItem>? ActionTemplate { get; set; }
    [Parameter] public RenderFragment<TItem>? EditTemplate { get; set; }

    protected override void OnInitialized()
    {
        Title = string.IsNullOrEmpty(Title) ? ClientService.Title : Title;

        base.OnInitialized();
    }

    private void HandleClickItemText(ITreeItemData<TItem>? node)
    {
        if (ShowDialogFunc == null || node == null) return;

        ShowDialogFunc(node);
    }
}
```

## 文件 60/66 TreeGraph.Blazor.Shared/Trees/TreeHelper.cs

```csharp
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees;
/// <summary>
/// TreeSky 树节点图标与 TreeItemData 转换/查找扩展。
///
/// 遍历/查找全部使用显式栈/队列迭代，深树不会 StackOverflowException。
/// </summary>
public static class TreeHelper
{
    public const string TreeItemIcons = Icons.Material.Outlined.Label;

    public static TreeItemData<T> ToTreeItemData<T>(this TreeNodeDto<T> dto)
        where T : class, ITreeNodeBase<T>, new()
    {
        var item = new TreeItemData<T>
        {
            Icon = TreeItemIcons,
            Text = dto.Text,
            Value = dto.Value,
            Expanded = dto.Expanded,
            Selected = dto.Selected,
            Expandable = dto.HasChildren,
            Children = dto.Children?.Select(i => i.ToTreeItemData<T>()).ToList(),
        };

        // dto.Value 可能为 null（懒加载占位 / 后端返回空值）
        if (item.Value is not null)
            item.Value.Parent = dto.Parent;

        return item;
    }

    /// <summary>
    /// 在树中查找指定 ID 的 TreeItemData 节点（显式栈 DFS）。
    /// </summary>
    public static TreeItemData<T>? FindTreeItem<T>(
        this IEnumerable<TreeItemData<T>> items,
        string id)
        where T : class, ITreeNodeBase<T>
    {
        if (string.IsNullOrEmpty(id)) return null;

        var stack = new Stack<TreeItemData<T>>(items.Reverse());

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current.Value?.Id == id)
                return current;

            if (current.Children is not { Count: > 0 }) continue;

            // 逆序压栈，保持与原递归一致的先左后右访问顺序
            var children = current.Children.OfType<TreeItemData<T>>().ToList();
            for (int i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);
        }

        return null;
    }

    /// <summary>
    /// 获取从根到目标节点的 ID 路径（根在前）。BFS + 父指针回溯，不递归。
    /// 节点若形成环，按已访问集合安全跳过。
    /// </summary>
    public static List<string>? GetPathToNode<T>(
        this IEnumerable<TreeItemData<T>> items,
        string targetId)
        where T : class, ITreeNodeBase<T>
    {
        if (string.IsNullOrEmpty(targetId)) return null;

        var parentMap = new Dictionary<string, string?>();
        var queue = new Queue<TreeItemData<T>>();

        foreach (var root in items)
        {
            if (root.Value is null) continue;
            parentMap.TryAdd(root.Value.Id, null);
            queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Value is null) continue;

            if (current.Value.Id == targetId)
            {
                var path = new List<string>();
                string? cursor = targetId;
                while (cursor is not null)
                {
                    path.Add(cursor);
                    cursor = parentMap.TryGetValue(cursor, out var parentId) ? parentId : null;
                }
                path.Reverse();
                return path;
            }

            if (current.Children is not { Count: > 0 }) continue;

            foreach (var child in current.Children)
            {
                if (child is not TreeItemData<T> { Value: not null } treeChild) continue;
                if (parentMap.ContainsKey(treeChild.Value.Id)) continue; // 防环

                parentMap[treeChild.Value.Id] = current.Value.Id;
                queue.Enqueue(treeChild);
            }
        }

        return null;
    }

    /// <summary>
    /// 展开指定节点并加载其子节点（单层）。
    /// </summary>
    public static async Task ExpandAsync<T>(
        this TreeItemData<T> item,
        Func<T?, Task<IReadOnlyCollection<TreeItemData<T>>>> loadChildren,
        CancellationToken ct = default)
        where T : class, ITreeNodeBase<T>
    {
        item.Expanded = true;

        if (item.Children == null || item.Children.Count == 0)
        {
            var children = await loadChildren(item.Value);
            ct.ThrowIfCancellationRequested();

            // TreeItemData<T> 未重写 Equals/GetHashCode，
            // ToHashSet 退化为按引用去重，等价于 ToList。
            item.Children = children.ToList<ITreeItemData<T>>();
        }
    }

    /// <summary>
    /// 沿给定路径逐层展开到目标节点（路径迭代，不递归）。
    /// </summary>
    public static async Task ExpandToNodeAsync<T>(
        this List<TreeItemData<T>> items,
        List<string> path,
        Func<T?, Task<IReadOnlyCollection<TreeItemData<T>>>> loadChildren,
        Action<T?>? onSelected = null,
        CancellationToken ct = default)
        where T : class, ITreeNodeBase<T>
    {
        var currentItems = items;

        for (int i = 0; i < path.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var nodeId = path[i];
            var item = currentItems.FirstOrDefault(x => x.Value?.Id == nodeId);
            if (item == null) break;

            // 目标节点本身只选中，不展开/加载它的子节点
            if (i == path.Count - 1)
            {
                onSelected?.Invoke(item.Value);
                break;
            }

            await item.ExpandAsync(loadChildren, ct);

            currentItems = item.Children?
                .OfType<TreeItemData<T>>()
                .ToList() ?? new List<TreeItemData<T>>();
        }
    }
}
```

## 文件 61/66 TreeGraph.Blazor.Shared/Trees/TreeSelectDialogSky.razor

```razor
@namespace TreeGraph.Blazor.Shared.Trees
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>, new()

@inject ITreeClientService<TItem> ClientService
@inject ISnackbar Snackbar

@* 精简移植版：移除了与 UnitTree 体系耦合的 EditFunc/编辑按钮；补齐移动场景的选择确认逻辑 *@
<DialogPageSky Title="@Title" MaxWidth="@MaxWidth"
               TitleClass="mud-theme-success px-0 pa-0">
    <ToolBarContent>
        @if (!string.IsNullOrEmpty(TitleToolBar))
        {
            <MudText Typo="Typo.h6" Class="text-info">@TitleToolBar</MudText>
        }

        @if (ToolBarLeftContent != null)
        {
            @ToolBarLeftContent
        }

        @* 移动场景（OriginalNode 非空）：允许直接把节点移到根层 *@
        @if (OriginalNode != null)
        {
            <MudButton Variant="Variant.Text"
                       Color="Color.Warning"
                       StartIcon="@Icons.Material.Filled.Publish"
                       OnClick="SelectRootAsync">
                移到根节点
            </MudButton>
        }

        <MudSpacer/>

        @if (ToolBarRightContent != null)
        {
            @ToolBarRightContent
        }
    </ToolBarContent>
    <DialogContent>
        <MudContainer MaxWidth="@MaxWidth">
            <TreeSky TItem="@TItem"
                     IsSelectDialog="true"
                     RootId="@RootId"
                     ClickNodeId="@ClickNodeId"
                     OnClickItemText="@HandleClickItemText"/>
        </MudContainer>
    </DialogContent>
</DialogPageSky>

@code {
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public ITreeItemData<TItem>? OriginalNode { get; set; }

    [Parameter] public string Title { get; set; } = "请选择上级节点";
    [Parameter] public string? TitleToolBar { get; set; } = "请选择";
    [Parameter] public string? RootId { get; set; }
    [Parameter] public string? ClickNodeId { get; set; }
    [Parameter] public MaxWidth MaxWidth { get; set; } = MaxWidth.Small;

    [Parameter] public RenderFragment? TopBarRightContent { get; set; }
    [Parameter] public RenderFragment? ToolBarLeftContent { get; set; }
    [Parameter] public RenderFragment? ToolBarRightContent { get; set; }

    /// <summary>叶子选择约束（来自 ITreeClientService.SelectLeaf），与是否为移动场景无关。</summary>
    private bool _selectLeaf;

    protected override void OnInitialized()
    {
        Title = string.IsNullOrEmpty(Title) ? ClientService.Title : Title;

        _selectLeaf = ClientService.SelectLeaf;

        base.OnInitialized();
    }

    private void HandleClickItemText(ITreeItemData<TItem>? node)
    {
        if (node?.Value == null) return;

        // 叶子约束：SelectLeaf=true 时目录节点仅供展开浏览，不可选
        if (_selectLeaf && node.Value.CanHaveChildren) return;

        var originalId = OriginalNode?.Value?.Id;
        if (originalId != null)
        {
            // 移动场景：禁止选择节点自身
            if (node.Value.Id == originalId)
            {
                Snackbar.Add("不能移动到自身下面", Severity.Warning);
                return;
            }

            // 禁止选择自身后代：沿候选节点的 Parent 链向上，若链上存在原节点则构成环
            var current = node.Value.Parent;
            while (current != null)
            {
                if (current.Id == originalId)
                {
                    Snackbar.Add("不能移动到自身的子节点下面", Severity.Warning);
                    return;
                }
                current = current.Parent;
            }
        }

        MudDialog?.Close(DialogResult.Ok(node.Value));
    }

    private async Task SelectRootAsync()
    {
        var roots = await ClientService.LoadInitialDataAsync(null);
        var root = roots.FirstOrDefault()?.Value;
        if (root != null)
            MudDialog?.Close(DialogResult.Ok(root));
    }
}
```

## 文件 62/66 TreeGraph.Blazor.Shared/Trees/TreeSky.razor

```razor
@* TreeSky.razor *@
@namespace TreeGraph.Blazor.Shared.Trees
@typeparam TItem where TItem : class, ITreeNodeBase<TItem>, new()

@inject ITreeNavigationHistoryService History
@inject IDialogService DialogService
@inject ISnackbar Snackbar
@inject BlazorService BlazorService
@inject ITreeClientService<TItem> ClientService
@inject TreeNodeDialogService<TItem> NodeDialogSvc
@inject MessageService Message

@if (_items == null || _items.Count == 0 || _loading)
{
    <ProgressCircularSky/>
}
else
{
    <MudTreeView T="TItem" ServerData="@(p => LoadChildrenAsync(p))" Items="@_items"
                 @bind-SelectedValue="SelectedValue"
                 SelectionMode="@SelectionMode.ToggleSelection">
        <ItemTemplate>
            <MudTreeViewItem T="TItem"
                             Text="@context.Text"
                             Value="@context.Value"
                             @bind-Items="context.Children"
                             @bind-Expanded="@context.Expanded"
                             CanExpand="@context.Expandable"
                             Icon="@context.Icon"
                             IconColor="@Color.Info"
                             LoadingIconColor="Color.Info"
                             OnClick="@(() => ClickItemText(context))">
                <BodyContent Context="node">
                    <MudContainer class="d-flex justify-space-between mx-0 px-0">
                        <MudText>
                            <MudHighlighter Text="@(context.Text)"
                                            HighlightedText="@HighlightedText"
                                            Class="mud-theme-primary"/>
                        </MudText>

                        @if (!IsSelectDialog)
                        {
                            <MudIconButton Icon="@Icons.Material.Filled.MoreHoriz"
                                           Size="Size.Small"
                                           Color="Color.Primary"
                                           OnClick="@(() => ShowNodeActionsAsync(context))"/>
                        }
                    </MudContainer>
                </BodyContent>
            </MudTreeViewItem>
        </ItemTemplate>
    </MudTreeView>
}

@code {
    [Parameter] public bool IsSelectDialog { get; set; }
    // ========== 参数 ==========
    [Parameter] public string? RootId { get; set; }
    [Parameter] public string? ClickNodeId { get; set; }

    [Parameter] public string? CurrentPage { get; set; }
    [Parameter] public EventCallback<string?> CurrentPageChanged { get; set; }

    [Parameter] public TItem? SelectedValue { get; set; }
    [Parameter] public EventCallback<TItem?> SelectedValueChanged { get; set; }

    [Parameter] public EventCallback<ITreeItemData<TItem>?> OnClickItemText { get; set; }

    [Parameter] public RenderFragment<TItem>? ActionTemplate { get; set; }
    [Parameter] public RenderFragment<TItem>? EditTemplate { get; set; }

    [Parameter] public Func<ITreeItemData<TItem>, Task>? ShowDialogFunc { get; set; }
    [Parameter] public EventCallback<bool> NewPageOpenChanged { get; set; }
}
```

## 文件 63/66 TreeGraph.Blazor.Shared/Trees/TreeSky.razor.Action.cs

```csharp
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.Contracts;
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees;

public partial class TreeSky<TItem>
{
    // ========== 节点操作回调（由外部页面处理具体业务） ==========
    private async Task ShowAddChildActionsAsync(ITreeItemData<TItem> node)
    {
        await ExecuteNodeActionAsync(NodeAction.AddChild, node);
    }

    private async Task ShowNodeActionsAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null) return;

        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            ActionTemplate = ActionTemplate,
        };

        await HandleNodeActionAsync(nodeTemplate);
    }

    private async Task HandleNodeActionAsync(NodeTemplate<TItem> nodeTemplate)
    {
        if (nodeTemplate.Node.Value == null) return;

        var parent = GetParentNode(nodeTemplate.Node.Value.ParentId!);
        var isBoot = nodeTemplate.Node.Value!.Id == RootId;

        var result = await NodeDialogSvc.ShowActionsDialogAsync(nodeTemplate, parent, isBoot);
        if (result == null) return;

        await ExecuteNodeActionAsync(result.Action, nodeTemplate.Node);
    }

    public async Task ExecuteNodeActionAsync(NodeAction action, ITreeItemData<TItem> node)
    {
        switch (action)
        {
            case NodeAction.View:
                await NodeDialogSvc.ShowViewDialogAsync(node.Value!);
                break;
            case NodeAction.AddChild:
                await HandleAddChildAsync(node);
                break;
            case NodeAction.Edit:
                await HandleEditAsync(node);
                break;
            case NodeAction.Delete:
                await HandleDeleteAsync(node);
                break;
            case NodeAction.Move:
                await HandleMoveNodeAsync(node);
                break;
            case NodeAction.Attribute:
                await HandleAttributeAsync(node);
                break;
            case NodeAction.Sort:
                await HandleSortAsync(node);
                break;
        }
    }

    #region CRUD 操作

    private async Task HandleAddChildAsync(ITreeItemData<TItem> parent)
    {
        var node = new TreeItemData<TItem>
        {
            Value = new TItem(),
        };
        node.Value!.ParentId = parent.Value!.Id;
        node.Value.Parent = parent.Value;

        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            EditTemplate = EditTemplate,
        };

        var formModel = await NodeDialogSvc.ShowCreateDialogAsync(nodeTemplate);
        if (formModel == null) return;
        if (_cts.IsCancellationRequested) return;

        try
        {
            var created = await ActionHandler.CreateChildAsync(
                parent.Value!, formModel, _cts.Token);
            if (created is null) return;

            parent.Expanded = true;
            await RefreshNodeChildrenAsync(parent, _cts.Token);
            Message.Success("创建成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("创建失败", e.Message);
        }
    }

    private async Task HandleEditAsync(ITreeItemData<TItem> node)
    {
        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            EditTemplate = EditTemplate,
        };

        var formModel = await NodeDialogSvc.ShowEditDialogAsync(nodeTemplate);
        if (formModel == null) return;

        try
        {
            var updated = await ActionHandler.UpdateNodeAsync(formModel, _cts.Token);
            if (updated is null) return;

            node.Text = formModel.Text();
            Message.Success("更新成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("更新失败", e.Message);
        }
    }

    private async Task HandleDeleteAsync(ITreeItemData<TItem> node)
    {
        var hasChildren = node.Children?.Count > 0 || node.Value?.HasChildren == true;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除节点【{node.Value?.Text()}】吗？",
            "删除", "取消");

        if (confirmed == null || !confirmed.Value) return;

        try
        {
            var ok = await ActionHandler.DeleteNodeAsync(node.Value!, _cts.Token);
            if (!ok)
            {
                Message.Warning("删除失败，节点不存在");
                return;
            }

            var parent = _items?.FindTreeItem(node.Value!.ParentId!);
            await RefreshNodeChildrenAsync(parent!, _cts.Token);

            Message.Success("删除成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("删除失败", e.Message);
        }
    }

    private async Task AddRootNodeAsync()
    {
        var formModel = await NodeDialogSvc.ShowCreateDialogAsync();
        if (formModel == null) return;

        await RefreshTreeAsync(_cts.Token);
        Message.Success("根分类已创建");
    }

    #endregion

    #region 移动与排序

    private async Task HandleMoveNodeAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null) return;

        var selectResult = await NodeDialogSvc.ShowParentSelectDialogAsync(node);

        if (selectResult == null) return;

        var message = $"节点【{node.Text}】的上级节点：【{node.Value.Parent?.Text()}】 => 【{selectResult.Text()}】!";
        var result = await Message.BoolBoxAsync(message);

        if (!result) return;

        try
        {
            // 后端 Update 不改 ParentId，移动必须走专门的 move 路由（含防环校验）
            var ok = await ActionHandler.MoveNodeAsync(
                node.Value, selectResult, _cts.Token);
            if (!ok)
            {
                Message.Warning("移动失败，可能节点不存在或试图移动到自身子节点下");
                return;
            }

            // API 成功后再同步内存（此前先改 ParentId 在失败时会造成内存与后端不一致）
            node.Value.ParentId = selectResult.Id;
            node.Value.Parent = selectResult;

            await ReLoadingAsync(node, _cts.Token);

            Message.Success("转移成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("转移失败", e.Message);
        }
    }

    /// <summary>
    /// 节点属性：属性子系统（NodeAttributesDialog 等）未包含在本次精简移植范围内。
    /// </summary>
    private Task HandleAttributeAsync(ITreeItemData<TItem> node)
    {
        Message.Info("节点属性模块未包含在 TreeSky 精简组件库中。");
        return Task.CompletedTask;
    }

    private async Task HandleSortAsync(ITreeItemData<TItem> node)
    {
        // 返回列表的顺序即用户拖拽后的新顺序（后端按列表位置重新编号 SortOrder）
        var sortResult = await NodeDialogSvc.ShowSortDialogAsync(node);

        if (sortResult == null) return;

        try
        {
            var ok = await ActionHandler.SortChildrenAsync(
                node.Value!, sortResult, _cts.Token);
            if (!ok) return;

            await RefreshNodeChildrenAsync(node, _cts.Token);

            Message.Success($"排序成功");
        }
        catch (OperationCanceledException) { }
    }

    #endregion

    #region 通用辅助方法

    private async Task ExecuteTreeOperationAsync(
        Func<Task> operation, string successMessage, CancellationToken ct = default)
    {
        await operation();
        await RefreshTreeAsync(ct);
        Message.Success(successMessage);
    }

    #endregion

    #region 数据加载

    private static IReadOnlyList<TreeNodeDto<TItem>> OrderNodes(IEnumerable<TreeNodeDto<TItem>> items)
        => items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text).ToList();

    private async Task RefreshTreeAsync(CancellationToken ct = default)
    {
        await RefreshAsync(ct);
        SelectedValue = null;
    }

    #endregion
}
```

## 文件 64/66 TreeGraph.Blazor.Shared/Trees/TreeSky.razor.cs

```csharp
using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.Models;
using TreeGraph.Blazor.Shared.Trees.Navigation;
using TreeGraph.Blazor.Shared.Trees.Services;

namespace TreeGraph.Blazor.Shared.Trees;

public partial class TreeSky<TItem> : IDisposable
{
    private List<TreeItemData<TItem>>? _items;
    private string? _lastClickNodeId;
    private string HighlightedText { get; set; } = string.Empty;

    private bool _loading = true;

    /// <summary>
    /// 组件级取消令牌：组件卸载 / 页面导航时取消所有进行中的异步操作。
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ITreeActionHandler<TItem> ActionHandler { get; set; } = default!;

    // ========== 生命周期 ==========
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _items = await LoadInitialDataAsync(_cts.Token);

            // 恢复选中（深层节点会沿祖先路径懒加载展开）
            await SetSelectedAsync(_cts.Token);

            if (ShowDialogFunc == null)
            {
                // 获取当前路径的第一个段作为页面标识
                var relative = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
                CurrentPage = relative.Split('/').FirstOrDefault();
                _ = CurrentPageChanged.InvokeAsync(CurrentPage);
            }
        }
        catch (OperationCanceledException)
        {
            // 组件卸载 / 导航取消，静默忽略
        }
        catch (Exception e)
        {
            Message.Details("数据加载失败。", e.Message);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_cts.IsCancellationRequested) return;

        if (!string.IsNullOrEmpty(ClickNodeId) && ClickNodeId != _lastClickNodeId)
        {
            _lastClickNodeId = ClickNodeId;
            try
            {
                await ExpandToNodeAsync(ClickNodeId, ct: _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 导航切换，静默忽略
            }
        }

        if (RootId == null && _items != null) RootId = _items!.FirstOrDefault()?.Value?.Id;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 深层书签/刷新直达：等 MudTreeView 挂载完成后再沿路径展开并选中
        if (firstRender && _pendingDeepClickNodeId is { } targetId)
        {
            _pendingDeepClickNodeId = null;
            try
            {
                await ExpandToNodeAsync(targetId, clearSelection: false, ct: _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 组件已卸载，静默忽略
            }
        }
    }

    // ========== IDisposable ==========
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
```

## 文件 65/66 TreeGraph.Blazor.Shared/Trees/TreeSky.razor.Loading.cs

```csharp
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees;

public partial class TreeSky<TItem>
{
    // ========== 数据加载 ==========
    private async Task<List<TreeItemData<TItem>>> LoadInitialDataAsync(
        CancellationToken ct = default)
    {
        try
        {
            _loading = true;

            var items = await ClientService.LoadInitialDataAsync(RootId, ct);
            ct.ThrowIfCancellationRequested();

            _loading = false;

            return items.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
        catch (OperationCanceledException)
        {
            _loading = false;
            throw;
        }
        catch (Exception e)
        {
            Message.Details("加载初始数据失败。", e.Message);
            return [];
        }
    }

    private async Task<IReadOnlyCollection<TreeItemData<TItem>>> LoadChildrenAsync(
        TItem? parent, CancellationToken ct = default)
    {
        if (parent == null)
        {
            var roots = await ClientService.LoadChildrenAsync(null, ct);
            return roots.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
        else
        {
            var children = await ClientService.LoadChildrenAsync(parent, ct);
            return children.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
    }

    // ========== 刷新 ==========
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var rootItems = await ClientService.LoadChildrenAsync(null, ct);
        ct.ThrowIfCancellationRequested();

        _items = rootItems?.Select(x => x.ToTreeItemData<TItem>()).ToList() ?? [];
        StateHasChanged();
    }

    private async Task RefreshNodeChildrenAsync(
        ITreeItemData<TItem> node, CancellationToken ct = default)
    {
        SelectedValue = null;
        _ = SelectedValueChanged.InvokeAsync(null);

        StateHasChanged();

        var children = await ClientService.LoadChildrenAsync(node.Value, ct);
        ct.ThrowIfCancellationRequested();

        node.Children = children.Select(c => new TreeItemData<TItem>
        {
            Value = c.Value,
            Text = c.Text,
            Icon = TreeHelper.TreeItemIcons,
            Expandable = c.HasChildren,
            Expanded = false,
            Children = c.Children?.Select(x => x.ToTreeItemData<TItem>()).ToList()
        }).ToList();

        node.Expanded = true;
        SelectedValue = node.Value;
        _ = SelectedValueChanged.InvokeAsync(node.Value);

        StateHasChanged();
    }

    private async Task ReLoadingAsync(
        ITreeItemData<TItem> node, CancellationToken ct = default)
    {
        _items = await LoadInitialDataAsync(ct);

        await ExpandToNodeAsync(node.Value!.Id, ct: ct);

        StateHasChanged();
    }
}
```

## 文件 66/66 TreeGraph.Blazor.Shared/Trees/TreeSky.razor.Node.cs

```csharp
using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Blazor.Shared.Trees;

public partial class TreeSky<TItem>
{
    // ========== 节点展开 ==========
    private async Task ExpandToNodeAsync(
        string targetId, bool clearSelection = true, CancellationToken ct = default)
    {
        var path = _items?.GetPathToNode(targetId);

        if (path == null)
        {
            path = await GetAncestorPathFromApiAsync(targetId, ct);
        }

        ct.ThrowIfCancellationRequested();

        if (path is not { Count: > 0 }) return;

        // 导航切换目标时清空旧选中；初始挂载沿路径恢复时不能清空
        // （中间渲染会让 MudTreeView 在目标子树挂载前固化空选中并回写）
        if (clearSelection)
        {
            SelectedValue = null;
            _ = SelectedValueChanged.InvokeAsync(null);
            StateHasChanged();
        }

        await _items!.ExpandToNodeAsync(
            path: path,
            loadChildren: item => LoadChildrenAsync(item, ct),
            onSelected: value =>
            {
                SelectedValue = value;
                _ = SelectedValueChanged.InvokeAsync(value);
            },
            ct: ct);

        StateHasChanged();
    }

    private async Task<List<string>?> GetAncestorPathFromApiAsync(
        string nodeId, CancellationToken ct = default)
    {
        return await ClientService.GetAncestorPathFromApiAsync(nodeId, ct);
    }

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

    private string? _pendingDeepClickNodeId;

    /// <summary>
    /// 从 ClickNodeId 恢复选中状态（首屏阶段）。
    /// 目标已在首屏节点中时直接选中；深层节点登记到 <see cref="_pendingDeepClickNodeId"/>，
    /// 等首帧渲染（MudTreeView 挂载）完成后再沿祖先路径懒加载展开，
    /// 避免在树视图挂载边界内设置选中值被其初始化流程重置。
    /// </summary>
    private Task SetSelectedAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(ClickNodeId)) return Task.CompletedTask;
        if (_items is not { Count: > 0 }) return Task.CompletedTask;

        var found = FindNodeById(_items, ClickNodeId);
        if (found is not null)
        {
            SelectedValue = found;
            _ = SelectedValueChanged.InvokeAsync(found);
            _lastClickNodeId = ClickNodeId;
            return Task.CompletedTask;
        }

        // 深层节点：延后到首帧渲染后处理；同时阻止 OnParametersSetAsync 重复展开
        _pendingDeepClickNodeId = ClickNodeId;
        _lastClickNodeId = ClickNodeId;
        return Task.CompletedTask;
    }

    // ========== 节点查找（显式栈迭代） ==========
    private TItem? FindNodeById(IEnumerable<ITreeItemData<TItem>> items, string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var stack = new Stack<ITreeItemData<TItem>>(items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.Value?.Id == id) return current.Value;

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
                stack.Push(child);
        }

        return null;
    }

    /// <summary>
    /// 从树中查找指定节点的父节点（显式栈 DFS）。
    /// </summary>
    private TItem? GetParentNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || _items is null) return null;

        var stack = new Stack<ITreeItemData<TItem>>(_items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current.Children?.Any(c => c.Value?.Id == nodeId) == true)
                return current.Value;

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
                stack.Push(child);
        }

        return null;
    }

    /// <summary>
    /// 从树中移除指定节点（含从父节点 Children 集合摘除）。
    /// 显式栈迭代；栈帧携带实际列表引用与属主节点，保证深层移除能回写父节点。
    /// </summary>
    public bool RemoveNodeFromParent(List<TreeItemData<TItem>> items, string id)
    {
        if (string.IsNullOrEmpty(id) || items.Count == 0) return false;

        // 用非泛型 IList 作栈帧：根列表是 List<TreeItemData<T>>，
        // 子层列表是 List<ITreeItemData<T>>，两者都实现非泛型 IList，
        // 且帧内持有的是真实引用，深层移除可直接回写。
        var stack = new Stack<(System.Collections.IList Level, ITreeItemData<TItem>? Owner)>();
        stack.Push((items, null));

        while (stack.Count > 0)
        {
            var (level, owner) = stack.Pop();

            for (int i = 0; i < level.Count; i++)
            {
                var node = (ITreeItemData<TItem>)level[i]!;

                if (node.Value?.Id == id)
                {
                    level.RemoveAt(i);

                    // 回写属主（子层列表一定是 List<ITreeItemData>）并更新 HasChildren
                    if (owner is not null)
                    {
                        owner.Children = (List<ITreeItemData<TItem>>)level;
                        if (owner.Value is not null)
                            owner.Value.HasChildren = level.Count > 0;
                    }
                    return true;
                }
            }

            // 逆序压栈保持先左后右
            for (int i = level.Count - 1; i >= 0; i--)
            {
                var node = (ITreeItemData<TItem>)level[i]!;
                if (node.Children is not { Count: > 0 }) continue;

                if (node.Children is not System.Collections.IList childLevel)
                {
                    var materialized = node.Children
                        .OfType<TreeItemData<TItem>>()
                        .ToList<ITreeItemData<TItem>>();
                    node.Children = materialized;
                    childLevel = materialized;
                }

                stack.Push((childLevel, node));
            }
        }

        return false;
    }

    #region 获取所有节点

    /// <summary>
    /// 只读：返回当前已加载的全部节点值（扁平化），不触发任何懒加载。
    /// </summary>
    public List<TItem> GetAllLoadedNodes()
    {
        var result = new List<TItem>();
        if (_items is null) return result;

        var stack = new Stack<ITreeItemData<TItem>>(_items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.Value is not null) result.Add(current.Value);

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
                stack.Push(child);
        }

        return result;
    }

    /// <summary>
    /// 确保全部节点已加载后返回扁平列表。会沿树触发懒加载 API 调用（有副作用）；
    /// 只读场景请用 <see cref="GetAllLoadedNodes"/>。
    /// </summary>
    public async Task<List<TItem>> EnsureAllNodesLoadedAsync(CancellationToken ct = default)
    {
        var result = new List<TItem>();
        if (_items is null) return result;

        var queue = new Queue<ITreeItemData<TItem>>(_items);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = queue.Dequeue();
            if (current.Value is not null) result.Add(current.Value);

            // 未展开但有子节点 → 触发懒加载
            if (current.Children?.Any() != true && current.Value?.HasChildren == true)
            {
                var children = await LoadChildrenAsync(current.Value, ct);
                ct.ThrowIfCancellationRequested();
                current.Children = children.ToList<ITreeItemData<TItem>>();
            }

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
                queue.Enqueue(child);
        }

        return result;
    }

    #endregion
}
```

