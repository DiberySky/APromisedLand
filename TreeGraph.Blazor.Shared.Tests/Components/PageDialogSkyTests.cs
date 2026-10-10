using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Common;
using TreeGraph.Blazor.Shared.Platform;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Components;

/// <summary>
/// PageDialogSky 移动端适配测试（bUnit + FakePlatformContext）。
/// MudDialog 脱离 MudDialogProvider 渲染为空，故经真实 IDialogService 打开。
/// 全部通过 Fake 控制形态，不依赖 JS（Loose 模式返回默认值 0 不可信）。
/// </summary>
public class PageDialogSkyTests : BunitTestBase {
    private readonly FakePlatformContext _platform = new();

    public PageDialogSkyTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IPlatformContext>(_platform);
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenDialogAsync(
        DialogParameters? parameters = null)
    {
        var (provider, _) = await OpenDialogWithRefAsync(parameters);
        return provider;
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Reference)>
        OpenDialogWithRefAsync(
            DialogParameters? parameters = null,
            DialogOptions? options = null)
    {
        var provider = Render<MudDialogProvider>();

        var p = parameters ?? new DialogParameters();
        p.Add("Title", "测试对话框");
        var dialogService = Services.GetRequiredService<IDialogService>();
        var reference = await dialogService.ShowAsync<PageDialogSky>(
            "测试对话框", p, options ?? new DialogOptions());
        provider.Render();

        return (provider, reference);
    }

    private void SetMobile()
    {
        _platform.FormFactor = PlatformFormFactor.Phone;
        _platform.IsMobile = true;
        _platform.IsCompact = true;
    }

    // ============================================================
    // 1. 桌面 + 全默认：Row 布局、无全屏/满宽
    // ============================================================

    [Fact]
    public async Task Desktop_Default_RowLayout_NoFullScreen()
    {
        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "SubmitButtonVisible", true },
        });

        var dialog = provider.Find(".mud-dialog");
        Assert.DoesNotContain("mud-dialog-fullscreen", dialog.ClassName);
        // Row 布局：MudStack Row 渲染为 d-flex flex-row；有 MudContainer 包裹
        Assert.NotNull(provider.Find(".mud-dialog-actions .mud-container"));
        Assert.NotEmpty(provider.FindAll(".mud-dialog-actions .mud-container .d-flex.flex-row"));
    }

    // ============================================================
    // 2. 手机 + 全默认：FullScreen、FullWidth、Column
    // ============================================================

    [Fact]
    public async Task Mobile_Default_ColumnLayout()
    {
        SetMobile();

        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "SubmitButtonVisible", true },
        });

        // Column 布局：无 MudContainer，无 row stack，存在 column stack；按钮满宽
        // （MudBlazor 9：MudButton FullWidth 渲染为 mud-width-full）
        Assert.Empty(provider.FindAll(".mud-dialog-actions .mud-container"));
        Assert.Empty(provider.FindAll(".mud-dialog-actions .d-flex.flex-row"));
        Assert.NotEmpty(provider.FindAll(".mud-dialog-actions .d-flex.flex-column"));
        Assert.NotEmpty(provider.FindAll(".mud-dialog-actions button.mud-width-full"));
    }

    // ============================================================
    // 3. 手机 + FullScreen=false：显式覆盖
    // ============================================================

    [Fact]
    public async Task Mobile_FullScreenFalse_NotFullScreen()
    {
        SetMobile();

        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "FullScreen", false },
        });

        Assert.DoesNotContain("mud-dialog-fullscreen",
            provider.Find(".mud-dialog").ClassName);
    }

    // ============================================================
    // 4. 桌面 + FullScreen=true：用户强制全屏
    // ============================================================

    [Fact]
    public async Task Desktop_FullScreenTrue_NoMobileLayout()
    {
        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "FullScreen", true },
            { "SubmitButtonVisible", true },
        });

        // FullScreen 只影响对话框尺寸（由 MudDialog.Options 控制），
        // 不应错误触发移动端 Column 分支；桌面仍保持 Row 布局
        Assert.NotEmpty(provider.FindAll(".mud-dialog-actions .d-flex.flex-row"));
    }

    // ============================================================
    // 5. 手机 + 自定义 DialogActions：原样渲染，不渲染默认按钮
    // ============================================================

    [Fact]
    public async Task Mobile_CustomDialogActions_RendersAsIs()
    {
        SetMobile();

        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "SubmitButtonVisible", true },
            { "DialogActions", (RenderFragment)(b => b.AddContent(0, "自定义动作区XYZ")) },
        });

        Assert.Contains("自定义动作区XYZ", provider.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("提交", provider.Find(".mud-dialog-actions").TextContent,
            StringComparison.Ordinal);
    }

    // ============================================================
    // 6. 手机 + DialogLeftActions/DialogRightActions：Column 流中均渲染
    // ============================================================

    [Fact]
    public async Task Mobile_LeftAndRightActions_BothRendered()
    {
        SetMobile();

        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "DialogLeftActions", (RenderFragment)(b => b.AddContent(0, "左侧动作AAA")) },
            { "DialogRightActions", (RenderFragment)(b => b.AddContent(0, "右侧动作BBB")) },
        });

        var actions = provider.Find(".mud-dialog-actions").TextContent;
        Assert.Contains("左侧动作AAA", actions, StringComparison.Ordinal);
        Assert.Contains("右侧动作BBB", actions, StringComparison.Ordinal);
    }

    // ============================================================
    // 7. 手机 + ContentClass：追加语义（主题类不丢失）
    // ============================================================

    [Fact]
    public async Task Mobile_ContentClass_AppendsNotReplaces()
    {
        SetMobile();

        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "ContentClass", "mud-theme-dark" },
        });

        var content = provider.Find(".mud-dialog-content");
        Assert.Contains("mud-theme-dark", content.ClassName, StringComparison.Ordinal);
        Assert.Contains("pa-3", content.ClassName, StringComparison.Ordinal);
    }

    // ============================================================
    // 8. 手机 + IconsVisible=true：Column 按钮含 StartIcon
    // ============================================================

    [Fact]
    public async Task Mobile_IconsVisible_ButtonsHaveStartIcon()
    {
        SetMobile();

        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "SubmitButtonVisible", true },
            { "IconsVisible", true },
        });

        var icons = provider.FindAll(".mud-dialog-actions button .mud-icon-root");
        Assert.NotEmpty(icons);
    }

    // ============================================================
    // 9. 手机 + ShowCloseIconInTitleOnMobile=false：无关闭图标
    // ============================================================

    [Fact]
    public async Task Mobile_HideCloseIcon_NoCloseButtonInTitle()
    {
        SetMobile();

        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "ShowCloseIconInTitleOnMobile", false },
            { "FullScreenToggleVisible", false },
        });

        Assert.Empty(provider.FindAll(".mud-dialog-title .mud-toolbar button:not(.dev-dialog-source-btn)"));
    }

    // ============================================================
    // 9b. 手机默认：标题栏有关闭图标
    // ============================================================

    [Fact]
    public async Task Mobile_Default_ShowsCloseIconInTitle()
    {
        SetMobile();

        var provider = await OpenDialogAsync();

        Assert.NotEmpty(provider.FindAll(".mud-dialog-title .mud-toolbar button"));
    }

    // ============================================================
    // 10. 手机 + CustomTitle：不渲染默认 toolbar 和关闭图标
    // ============================================================

    [Fact]
    public async Task Mobile_CustomTitle_NoDefaultToolbarNoCloseIcon()
    {
        SetMobile();

        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "CustomTitle", (RenderFragment)(b => b.AddContent(0, "自定义标题CCC")) },
        });

        Assert.Contains("自定义标题CCC", provider.Markup, StringComparison.Ordinal);
        Assert.Empty(provider.FindAll(".mud-dialog-title .mud-toolbar"));
    }

    // ============================================================
    // 11. 手机 + AutoDetectMobile=false：不进入移动端分支
    // ============================================================

    [Fact]
    public async Task Mobile_AutoDetectMobileFalse_StaysDesktop()
    {
        SetMobile();

        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "AutoDetectMobile", false },
            { "SubmitButtonVisible", true },
        });

        Assert.DoesNotContain("mud-dialog-fullscreen",
            provider.Find(".mud-dialog").ClassName);
        Assert.NotEmpty(provider.FindAll(".mud-dialog-actions .d-flex.flex-row"));
    }

    // ============================================================
    // 12. 首渲染后调用 Platform.RefreshAsync 一次
    // ============================================================

    [Fact]
    public async Task FirstRender_CallsRefreshAsyncOnce()
    {
        await OpenDialogAsync();

        Assert.Equal(1, _platform.RefreshCallCount);
    }

    // ============================================================
    // 13. RaiseChanged 后组件重渲染且布局切换
    // ============================================================

    [Fact]
    public async Task PlatformChanged_RerendersWithNewLayout()
    {
        // 初始桌面：Row
        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "SubmitButtonVisible", true },
        });
        Assert.NotEmpty(provider.FindAll(".mud-dialog-actions .d-flex.flex-row"));

        // 切到手机形态并触发 Changed
        SetMobile();
        _platform.RaiseChanged();

        provider.WaitForAssertion(() =>
        {
            Assert.Empty(provider.FindAll(".mud-dialog-actions .d-flex.flex-row"));
            Assert.NotEmpty(provider.FindAll(".mud-dialog-actions .d-flex.flex-column"));
        }, TimeSpan.FromSeconds(2));
    }

    // ============================================================
    // 14. 零回归断言：桌面+全默认时不调用 SetOptionsAsync（Options 引用不变）
    // ============================================================

    [Fact]
    public async Task Desktop_Default_DoesNotReplaceOptions()
    {
        var original = new DialogOptions { BackdropClick = false };

        var (_, reference) = await OpenDialogWithRefAsync(options: original);

        // 未调用 SetOptionsAsync：Options 保持原对象引用，BackdropClick 原样保留
        Assert.Same(original, ((PageDialogSky)reference.Dialog!).MudDialog!.Options);
        Assert.False(((PageDialogSky)reference.Dialog!).MudDialog!.Options.BackdropClick);
    }

    // ============================================================
    // 15. 移动端默认值替换 Options 时，保留调用方原有设置（#8 防漏字段）
    // ============================================================

    [Fact]
    public async Task Mobile_Default_PreservesCallerOptions()
    {
        SetMobile();
        var original = new DialogOptions
        {
            BackdropClick = false,
            CloseOnEscapeKey = false,
            CloseButton = true,
        };

        var (_, reference) = await OpenDialogWithRefAsync(options: original);

        // 移动端触发了整体替换（新实例），但调用方字段逐一保留
        var options = ((PageDialogSky)reference.Dialog!).MudDialog!.Options;
        Assert.NotSame(original, options);
        Assert.False(options.BackdropClick);
        Assert.False(options.CloseOnEscapeKey);
        Assert.True(options.CloseButton);
        // 移动端默认值生效
        Assert.True(options.FullScreen);
        Assert.True(options.FullWidth);
        Assert.Equal(MaxWidth.False, options.MaxWidth);
    }

    // ============================================================
    // 16. 并发：两个对话框同时打开，Options 互不影响（#5）
    // ============================================================

    [Fact]
    public async Task TwoDialogs_Concurrent_OptionsAreIndependent()
    {
        // 对话框 A：显式强制全屏；对话框 B：显式强制非全屏
        var (_, refA) = await OpenDialogWithRefAsync(
            new DialogParameters { { "FullScreen", true } });
        var (_, refB) = await OpenDialogWithRefAsync(
            new DialogParameters { { "FullScreen", false } });

        var optionsA = ((PageDialogSky)refA.Dialog!).MudDialog!.Options;
        var optionsB = ((PageDialogSky)refB.Dialog!).MudDialog!.Options;

        // 每个 DialogInstance 持有独立 Options，后开的 B 不污染先开的 A
        Assert.NotSame(optionsA, optionsB);
        Assert.True(optionsA.FullScreen);
        Assert.False(optionsB.FullScreen);
    }

    // ============================================================
    // 16b. 桌面 + DialogMaxWidth 显式传参：覆盖调用方 Options.MaxWidth
    //      （null 默认的零回归路径已由 #14 的引用相等断言覆盖）
    // ============================================================

    [Fact]
    public async Task Desktop_DialogMaxWidth_Explicit_OverridesOptions()
    {
        var original = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            BackdropClick = false,
        };

        var (_, reference) = await OpenDialogWithRefAsync(
            new DialogParameters { { "DialogMaxWidth", MaxWidth.Medium } },
            original);

        var options = ((PageDialogSky)reference.Dialog!).MudDialog!.Options;

        // 显式 DialogMaxWidth 生效，不再静默忽略
        Assert.Equal(MaxWidth.Medium, options.MaxWidth);
        // 触发了整体替换，但调用方其他字段逐一保留
        Assert.NotSame(original, options);
        Assert.False(options.BackdropClick);
    }

    // ============================================================
    // 16c. FullWidth=true + DialogMaxWidth 同时指定：FullWidth 优先
    //      （满宽时 MaxWidth 内部按 False 处理）
    // ============================================================

    [Fact]
    public async Task FullWidthTrue_BeatsDialogMaxWidth_MaxWidthFalse()
    {
        var original = new DialogOptions { MaxWidth = MaxWidth.Large };

        var (_, reference) = await OpenDialogWithRefAsync(
            new DialogParameters
            {
                { "FullWidth", true },
                { "DialogMaxWidth", MaxWidth.Medium },
            },
            original);

        var options = ((PageDialogSky)reference.Dialog!).MudDialog!.Options;

        Assert.True(options.FullWidth);
        Assert.Equal(MaxWidth.False, options.MaxWidth);
    }

    // ============================================================
    // 17. DialogOptions 属性数守卫：MudBlazor 升级新增字段时失败，
    //     提醒同步 PageDialogSky.ApplyDialogOptionsAsync 的手动拷贝列表
    // ============================================================

    [Fact]
    public void DialogOptions_PropertyCount_Guard()
    {
        // MudBlazor 9.11.0 共 11 个属性（record，已由 record with 自动覆盖）。
        // 若升级后数量变化，提醒复核 with 表达式的语义（新增属性通常已默认携带，无需手动追加）。
        var props = typeof(DialogOptions).GetProperties();
        Assert.Equal(11, props.Length);
        Assert.Equal(
            new[]
            {
                "BackdropClick", "BackgroundClass", "CloseButton",
                "CloseOnEscapeKey", "CloseOnNavigation", "DefaultFocus",
                "FullScreen", "FullWidth", "MaxWidth", "NoHeader", "Position",
            },
            props.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    // ============================================================
    // 18. SubmitButtonDisabled=true：提交按钮被禁用
    // ============================================================

    [Fact]
    public async Task SubmitButtonDisabled_True_SubmitButtonIsDisabled()
    {
        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "SubmitButtonVisible", true },
            { "SubmitButtonDisabled", true },
        });

        var submitButton = provider.FindAll(".mud-dialog-actions button")
            .FirstOrDefault(b => b.TextContent.Trim() == "提交");

        Assert.NotNull(submitButton);
        Assert.NotNull(submitButton!.GetAttribute("disabled"));
    }

    // ============================================================
    // 18b. SubmitButtonDisabled=false（默认）：提交按钮可用
    // ============================================================

    [Fact]
    public async Task SubmitButtonDisabled_Default_SubmitButtonIsEnabled()
    {
        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "SubmitButtonVisible", true },
        });

        var submitButton = provider.FindAll(".mud-dialog-actions button")
            .FirstOrDefault(b => b.TextContent.Trim() == "提交");

        Assert.NotNull(submitButton);
        Assert.Null(submitButton!.GetAttribute("disabled"));
    }

    // ============================================================
    // 19. 提交重试回归：SubmitButtonClosesDialog=false（默认）时校验失败后
    //     再次点击提交仍触发 OnSubmitClick（_closing 不得锁死不关闭路径）
    // ============================================================

    [Fact]
    public async Task Submit_Twice_WhenNotClosing_InvokesBothTimes()
    {
        var submitCount = 0;
        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "SubmitButtonVisible", true },
            { "OnSubmitClick", EventCallback.Factory.Create(this, () => submitCount++) },
        });

        var submitButton = provider.FindAll(".mud-dialog-actions button")
            .First(b => b.TextContent.Trim() == "提交");
        submitButton.Click();
        // 首次点击触发重渲染，需重新查找元素再点第二次
        provider.FindAll(".mud-dialog-actions button")
            .First(b => b.TextContent.Trim() == "提交").Click();

        Assert.Equal(2, submitCount);
    }

    // ============================================================
    // 20. 物理返回键重入：HandleBackButton 连触两次，OnCanceledClick 只触发一次
    // ============================================================

    [Fact]
    public async Task BackButton_Twice_InvokesCanceledOnce()
    {
        var cancelCount = 0;
        var (provider, reference) = await OpenDialogWithRefAsync(new DialogParameters
        {
            { "OnCanceledClick", EventCallback.Factory.Create(this, () => cancelCount++) },
        });

        var instance = (PageDialogSky)reference.Dialog!;
        // Close 内部触发 StateHasChanged，必须切回渲染器 Dispatcher
        await provider.InvokeAsync(() => instance.HandleBackButton());
        await provider.InvokeAsync(() => instance.HandleBackButton());

        Assert.Equal(1, cancelCount);
    }

    // ============================================================
    // 21. 返回箭头（非关闭语义）也受 _closing 保护：关闭流程中点击不再触发
    // ============================================================

    [Fact]
    public async Task ArrowBack_NotClosing_WhenClosingSet_NotInvoked()
    {
        var cancelCount = 0;
        var (provider, reference) = await OpenDialogWithRefAsync(new DialogParameters
        {
            { "ArrowBackVisible", true },
            { "ArrowBackClosesDialog", false },
            { "OnCanceledClick", EventCallback.Factory.Create(this, () => cancelCount++) },
        });

        // 模拟关闭流程已开始（_closing = true），但对话框仍显示
        var instance = (PageDialogSky)reference.Dialog!;
        typeof(PageDialogSky)
            .GetField("_closing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(instance, true);

        // 点返回箭头（ArrowBackClosesDialog=false 路径）：_closing 已置位，不得触发
        provider.Find(".mud-dialog-title .mud-toolbar button").Click();
        Assert.Equal(0, cancelCount);
    }

    // ============================================================
    // 22. FullScreen=true 显式传参：切换按钮初始即显示"还原"图标
    // ============================================================

    [Fact]
    public async Task FullScreenTrue_ToggleButton_ShowsRestoreIcon()
    {
        var provider = await OpenDialogAsync(new DialogParameters
        {
            { "FullScreen", true },
        });

        Assert.Contains(Icons.Material.Filled.FullscreenExit, provider.Markup,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Icons.Material.Filled.Fullscreen, provider.Markup,
            StringComparison.Ordinal);
    }
}
