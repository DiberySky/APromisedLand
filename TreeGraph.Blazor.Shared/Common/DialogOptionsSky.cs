using MudBlazor;
using MudBlazor.Extensions.Options;

namespace TreeGraph.Blazor.Shared.Common;

/// <summary>
/// 对话框选项工厂：封装常用打开参数，避免各调用点重复手写并踩坑。
/// </summary>
public static class DialogOptionsSky
{
    /// <summary>
    /// 全屏对话框选项（对齐 FilePreviewDialog 的全屏打开参数）。
    /// 关键点：MaxWidth = MaxWidth.False 解除最大宽度约束，否则打开时顶部会留空、
    /// 需手动点击最大化按钮才真正铺满视口。
    /// </summary>
    /// <param name="maximizeButton">是否显示最大化/还原按钮（默认 true）。</param>
    public static DialogOptionsEx FullScreen(bool maximizeButton = true)
        => new()
        {
            FullScreen = true,
            FullWidth = true,
            MaxWidth = MaxWidth.False,
            Position = DialogPosition.Center,
            MaximizeButton = maximizeButton,
            BackdropClick = false
        };
}
