using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Options;
using TreeGraph.Blazor.Shared.LineSky.Dialogs;
using TreeGraph.Blazor.Shared.LineSky.Services;
using TreeGraph.Blazor.Shared.TreeEavSky.Nodes;

namespace TreeGraph.Blazor.Shared.LineSky.Components;

/// <summary>宿主操作对话框返回的动作（按钮不直接执行，由编排循环分发）。</summary>
public enum LineNodeHostAction
{
    /// <summary>编辑（名称/描述/连接 + 属性区）。</summary>
    Edit,
    /// <summary>EAV 属性管理。</summary>
    Attribute,
    /// <summary>删除节点。</summary>
    Delete,
    /// <summary>跳转到所属线路的节点维护对话框。</summary>
    LineManage
}

/// <summary>宿主操作循环的最终结果，供调用方决定如何刷新。</summary>
public enum LineNodeHostOutcome
{
    /// <summary>用户关闭宿主且无任何变更。</summary>
    Canceled,
    /// <summary>发生过变更（编辑成功 / 线路维护返回），节点仍存在。</summary>
    Changed,
    /// <summary>节点已删除或已不存在（含循环重开时 404）。</summary>
    Deleted
}

/// <summary>
/// 线路节点行内操作（编辑/属性/详情/删除/线路维护）的共享实现，
/// 供 LineNodeManageDialog 行内与语义搜索结果菜单复用。
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

    /// <summary>
    /// 宿主操作模式（对齐 TreeNodeActionsDialog 编排）：
    /// 打开 LineNodeActionsDialog 宿主 → 按钮仅返回动作 → 本循环分发执行；
    /// 编辑/属性执行完重新拉取节点并重开宿主（连续操作），删除/线路维护/取消退出循环。
    /// </summary>
    /// <param name="showLineManage">是否提供「线路节点维护」跳转（在线路管理对话框内传 false）。</param>
    /// <returns>最终结果：Canceled / Changed / Deleted（调用方据此刷新列表或移除行）。</returns>
    public static async Task<LineNodeHostOutcome> OperateAsync(
        IDialogService dialogService,
        LineNodeManageApiClient api,
        string nodeId,
        string? lineName,
        bool showLineManage = false,
        ISnackbar? snackbar = null)
    {
        var changed = false;

        while (true)
        {
            // 每轮重拉：编辑后宿主内嵌信息需展示最新快照；节点被删则直接退出
            var node = await api.GetByIdAsync(nodeId);
            if (node is null)
            {
                snackbar?.Add("线路节点不存在或已被删除", Severity.Warning);
                return LineNodeHostOutcome.Deleted;
            }

            var parameters = new DialogParameters<LineNodeActionsDialog>
            {
                { x => x.Node, node },
                { x => x.LineName, lineName ?? "" },
                { x => x.ShowLineManage, showLineManage }
            };
            var dialog = await dialogService.ShowExAsync<LineNodeActionsDialog>(
                "节点操作", parameters, NestedOptions);
            var result = await dialog.Result;

            if (result is not { Canceled: false } || result.Data is not LineNodeHostAction action)
                return changed ? LineNodeHostOutcome.Changed : LineNodeHostOutcome.Canceled;

            switch (action)
            {
                case LineNodeHostAction.Edit:
                    if (await EditAsync(dialogService, node))
                        changed = true;
                    break;

                case LineNodeHostAction.Attribute:
                    // 属性变更不影响线路节点列表展示（名称/描述），不标 changed
                    await AttributesAsync(dialogService, nodeId);
                    break;

                case LineNodeHostAction.Delete:
                    var deleted = await DeleteAsync(dialogService, node);
                    // 对齐 TreeNode：删除（无论成功或取消确认）后退出循环
                    return deleted
                        ? LineNodeHostOutcome.Deleted
                        : (changed ? LineNodeHostOutcome.Changed : LineNodeHostOutcome.Canceled);

                case LineNodeHostAction.LineManage:
                    // 维护对话框可能改动/删除本节点，直接按已变更退出，由调用方整体刷新
                    await LineManageAsync(dialogService, node.LineId, lineName);
                    return LineNodeHostOutcome.Changed;
            }
        }
    }

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
