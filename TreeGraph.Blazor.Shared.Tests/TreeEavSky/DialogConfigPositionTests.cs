using MudBlazor;
using TreeGraph.Blazor.Shared.TreeEavSky.Dialogs;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.TreeEavSky;

/// <summary>
/// DialogConfig.ToDialogOptions 位置传递回归测试。
///
/// 背景：MudBlazor.Extensions 的 DialogOptionsEx 以 new 阴影声明了自己的 Position
/// （基槽 DialogOptions.Position 保持 null）。MudBlazor 渲染对话框定位 CSS 类时
/// 读的是基槽 —— 只写阴影槽会导致所有 ShowExAsync 对话框仍居中显示。
/// 本测试锁定「基槽必须也被写入」这一修复。
/// </summary>
public class DialogConfigPositionTests
{
    [Fact]
    public void ToDialogOptions_Position_ReachesMudBlazorBaseSlot()
    {
        var options = new DialogConfig { MaxWidth = MaxWidth.Medium }.ToDialogOptions();

        // 阴影槽（扩展自身使用）
        Assert.Equal(DialogPosition.TopCenter, options.Position);
        // 基槽（MudBlazor 渲染定位类时实际读取的槽位）——修复前为 null（居中）
        Assert.Equal(DialogPosition.TopCenter, ((DialogOptions)options).Position);
    }

    [Fact]
    public void ToDialogOptions_CustomPosition_ReachesBaseSlot()
    {
        var options = new DialogConfig
        {
            Position = DialogPosition.BottomRight
        }.ToDialogOptions();

        Assert.Equal(DialogPosition.BottomRight, ((DialogOptions)options).Position);
    }
}
