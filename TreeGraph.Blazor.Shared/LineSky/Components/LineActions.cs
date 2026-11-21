using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Options;
using TreeGraph.Blazor.Shared.LineSky.Dialogs;
using TreeGraph.Blazor.Shared.LineSky.Services;

namespace TreeGraph.Blazor.Shared.LineSky.Components;

/// <summary>线路只读宿主对话框返回的动作（按钮不直接执行，由编排循环分发）。</summary>
public enum LineHostAction
{
    /// <summary>设置修改（名称/描述 + 属性区）。</summary>
    Edit
}

/// <summary>
/// 线路「只读 → 设置修改」编排（对齐 LineNodeActions.OperateAsync）：
/// 打开 LineActionsDialog 只读宿主 → 动作仅返回 LineHostAction → 本循环分发执行；
/// 编辑完成或取消后重新拉取线路并重开只读宿主，连续操作直至关闭。
/// 只读层与编辑层分离，后续可按权限决定只开放只读层。
/// </summary>
public static class LineActions
{
    /// <summary>宿主/编辑对话框选项：顶部居中打开。</summary>
    private static readonly DialogOptionsEx HostOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        MaximizeButton = true,
        Position = DialogPosition.TopCenter,
        BackdropClick = false
    };

    /// <returns>true = 发生过变更（调用方据此刷新列表）。</returns>
    public static async Task<bool> OperateAsync(
        IDialogService dialogService,
        LineManageApiClient api,
        string lineId,
        string? spaceName = null,
        ISnackbar? snackbar = null)
    {
        var changed = false;

        while (true)
        {
            // 每轮重拉：编辑后只读宿主需展示最新快照；线路被删则直接退出
            var line = await api.GetByIdAsync(lineId);
            if (line is null)
            {
                snackbar?.Add("线路不存在或已被删除", Severity.Warning);
                return changed;
            }

            var parameters = new DialogParameters<LineActionsDialog>
            {
                { x => x.Line, line },
                { x => x.SpaceName, spaceName }
            };
            var dialog = await dialogService.ShowExAsync<LineActionsDialog>(
                "线路内容", parameters, HostOptions);
            var result = await dialog.Result;

            if (result is not { Canceled: false } || result.Data is not LineHostAction action)
                return changed;

            switch (action)
            {
                case LineHostAction.Edit:
                    var editParameters = new DialogParameters<LineEditDialog>
                    {
                        { x => x.Line, line }
                    };
                    var editDialog = await dialogService.ShowExAsync<LineEditDialog>(
                        "设置修改", editParameters, HostOptions);
                    var editResult = await editDialog.Result;
                    if (editResult is { Canceled: false })
                        changed = true;
                    break;
            }
        }
    }
}
