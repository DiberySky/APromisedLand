using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Options;
using TreeGraph.Blazor.Shared.LineSky.Dialogs;
using TreeGraph.Blazor.Shared.TreeEavSky.Nodes;

namespace TreeGraph.Blazor.Shared.LineSky.Components;

/// <summary>
/// 线路节点行内操作（编辑/属性/详情/删除/线路维护）的共享实现，
/// 供 LineNodeActionsMenu 与语义搜索结果菜单复用。
/// 带返回值的方法：true = 已发生变更（调用方据此刷新）。
/// </summary>
public static class LineNodeActions
{
    /// <summary>二级弹窗选项（与 LineNodeManageDialog 一致）。</summary>
    private static readonly DialogOptionsEx NestedOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        MaximizeButton = true
    };

    /// <summary>编辑（名称/描述/连接 + 属性区）。</summary>
    public static async Task<bool> EditAsync(IDialogService dialogService, LineNodeDto node)
    {
        var parameters = new DialogParameters<LineNodeEditDialog>
        {
            { x => x.Node, node }
        };
        var dialog = await dialogService.ShowExAsync<LineNodeEditDialog>(
            "编辑线路节点", parameters, NestedOptions);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    /// <summary>删除（守卫链：段边/端子/属性/实体引用 → 409 透出）。</summary>
    public static async Task<bool> DeleteAsync(IDialogService dialogService, LineNodeDto node)
    {
        var parameters = new DialogParameters<LineNodeDeleteDialog>
        {
            { x => x.Node, node }
        };
        var dialog = await dialogService.ShowExAsync<LineNodeDeleteDialog>(
            "删除线路节点", parameters, NestedOptions);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    /// <summary>只读详情（名称/描述/所属线路/前驱节点/创建时间）。</summary>
    public static async Task ViewAsync(IDialogService dialogService, string nodeId, string? lineName)
    {
        var parameters = new DialogParameters<LineNodeViewDialog>
        {
            { x => x.NodeId, nodeId },
            { x => x.LineName, lineName ?? "" }
        };
        var dialog = await dialogService.ShowExAsync<LineNodeViewDialog>(
            "线路节点详情", parameters, NestedOptions);
        await dialog.Result;
    }

    /// <summary>EAV 属性（宿主无关，仅需 NodeId）。</summary>
    public static async Task AttributesAsync(IDialogService dialogService, string nodeId)
    {
        var parameters = new DialogParameters<NodeAttributesDialog>
        {
            { x => x.NodeId, nodeId }
        };
        var options = new DialogOptionsEx
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            MaximizeButton = true
        };
        var dialog = await dialogService.ShowExAsync<NodeAttributesDialog>(
            "节点属性", parameters, options);
        await dialog.Result;
    }

    /// <summary>线路节点维护（打开所属线路的管理对话框）。</summary>
    public static async Task LineManageAsync(IDialogService dialogService, string lineId, string? lineName)
    {
        var parameters = new DialogParameters<LineNodeManageDialog>
        {
            { x => x.LineId, lineId },
            { x => x.LineName, lineName ?? "" }
        };
        var options = new DialogOptionsEx
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            MaximizeButton = true
        };
        var dialog = await dialogService.ShowExAsync<LineNodeManageDialog>(
            "线路节点维护", parameters, options);
        await dialog.Result;
    }
}
