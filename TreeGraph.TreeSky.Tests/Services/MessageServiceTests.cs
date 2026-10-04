using Moq;
using MudBlazor;
using TreeGraph.TreeSky;
using Xunit;

namespace TreeGraph.TreeSky.Tests.Services;

/// <summary>
/// MessageService 单元测试。
/// Mock IDialogService + ISnackbar，覆盖通知与确认框方法。
/// 注意：MudBlazor 9.11 中 ISnackbar.Add 为 4 参（末参 key 可空），
/// SnackbarOptions.OnClick 为 Func&lt;Snackbar, Task&gt;。
/// </summary>
public class MessageServiceTests
{
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly MessageService _sut;

    public MessageServiceTests()
    {
        _sut = new MessageService(_dialogService.Object, _snackbar.Object);
    }

    // ============================================================
    // Success / Warning / Info / Error：Snackbar.Add 调用
    // ============================================================

    [Fact]
    public void Success_CallsSnackbarWithCorrectSeverity()
    {
        _sut.Success("操作成功");

        _snackbar.Verify(s => s.Add(
                "操作成功",
                Severity.Success,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Success_NullMessage_UsesFallback()
    {
        _sut.Success(null);

        _snackbar.Verify(s => s.Add(
                "没有信息。",
                Severity.Success,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Warning_CallsSnackbarWithWarningSeverity()
    {
        _sut.Warning("警告");

        _snackbar.Verify(s => s.Add(
                "警告",
                Severity.Warning,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Warning_NullMessage_UsesFallback()
    {
        _sut.Warning(null);

        _snackbar.Verify(s => s.Add(
                "没有信息。",
                Severity.Warning,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Info_CallsSnackbarWithInfoSeverity()
    {
        _sut.Info("信息");

        _snackbar.Verify(s => s.Add(
                "信息",
                Severity.Info,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Error_CallsSnackbarWithErrorSeverity()
    {
        _sut.Error("错误信息");

        _snackbar.Verify(s => s.Add(
                "错误信息",
                Severity.Error,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void Error_NullMessage_UsesFallback()
    {
        _sut.Error(null);

        _snackbar.Verify(s => s.Add(
                "没有信息。",
                Severity.Error,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    // ============================================================
    // Details：Error + Action "查看" + OnClick 回调
    // ============================================================

    [Fact]
    public void Details_CallsSnackbarWithErrorAndAction()
    {
        SnackbarOptions? captured = null;

        _snackbar
            .Setup(s => s.Add(
                It.IsAny<string>(),
                It.IsAny<Severity>(),
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()))
            .Callback<string, Severity, Action<SnackbarOptions>, string?>(
                (_, _, configure, _) =>
                {
                    var opts = new SnackbarOptions(Severity.Normal, new SnackbarConfiguration());
                    configure(opts);
                    captured = opts;
                });

        _sut.Details("加载失败", "详细错误堆栈");

        Assert.NotNull(captured);
        Assert.Equal("查看", captured!.Action);
        Assert.Equal(Color.Info, captured.ActionColor);
        Assert.NotNull(captured.OnClick);
    }

    [Fact]
    public async Task Details_OnClickAction_ShowsMessageBox()
    {
        SnackbarOptions? captured = null;
        _snackbar
            .Setup(s => s.Add(
                It.IsAny<string>(),
                It.IsAny<Severity>(),
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string?>()))
            .Callback<string, Severity, Action<SnackbarOptions>, string?>(
                (_, _, configure, _) =>
                {
                    var opts = new SnackbarOptions(Severity.Normal, new SnackbarConfiguration());
                    configure(opts);
                    captured = opts;
                });

        _sut.Details("加载失败", "详细错误堆栈");

        // MudBlazor 9.11：OnClick 为 Func<Snackbar, Task>，lambda 忽略入参，传 null 即可
        await captured!.OnClick!(null!);

        _dialogService.Verify(s => s.ShowMessageBoxAsync(
                "加载失败",
                "详细错误堆栈",
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DialogOptions>()),
            Times.Once);
    }

    // ============================================================
    // BoolBoxAsync（result != null 判定）
    // ============================================================

    [Fact]
    public async Task BoolBoxAsync_UserConfirms_ReturnsTrue()
    {
        _dialogService
            .Setup(s => s.ShowMessageBoxAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(true);

        var result = await _sut.BoolBoxAsync("确认删除？", "请确认");

        Assert.True(result);
    }

    [Fact]
    public async Task BoolBoxAsync_UserCancels_ReturnsFalse()
    {
        _dialogService
            .Setup(s => s.ShowMessageBoxAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync((bool?)null);

        var result = await _sut.BoolBoxAsync("确认删除？", "请确认");

        Assert.False(result);
    }

    // ============================================================
    // DeleteBox（result ?? false 判定）
    // ============================================================

    [Fact]
    public async Task DeleteBox_UserConfirms_ReturnsTrue()
    {
        _dialogService
            .Setup(s => s.ShowMessageBoxAsync(
                It.IsAny<MessageBoxOptions>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(true);

        var result = await _sut.DeleteBox("确认删除？", "删除");

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteBox_UserCancels_ReturnsFalse()
    {
        _dialogService
            .Setup(s => s.ShowMessageBoxAsync(
                It.IsAny<MessageBoxOptions>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync((bool?)null);

        var result = await _sut.DeleteBox("确认删除？", "删除");

        Assert.False(result);
    }
}
