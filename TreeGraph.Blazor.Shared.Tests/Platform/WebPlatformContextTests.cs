using Bunit;
using Bunit.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using TreeGraph.Blazor.Shared.Platform;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Platform;

/// <summary>
/// WebPlatformContext 与平台注册测试。
/// bUnit JSInterop Loose 模式下 eval 返回默认值 0，因此：
/// - DI 解析/TryAdd 语义直接测
/// - RefreshAsync 的宽度分支通过 JSInterop Setup 精确控制
/// - SSR JS 不可用场景通过自定义 IJSRuntime 抛 InvalidOperationException 模拟
/// </summary>
public class WebPlatformContextTests : BunitTestBase {
    public WebPlatformContextTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void AddTreeGraphPlatform_RegistersWebPlatformContext()
    {
        Services.AddTreeGraphPlatform();

        var ctx = Services.GetRequiredService<IPlatformContext>();
        Assert.IsType<WebPlatformContext>(ctx);
        Assert.Equal("Server", ctx.HostKind);
    }

    [Fact]
    public void AddTreeGraphPlatform_TryAdd_DoesNotOverrideExisting()
    {
        var fake = new FakePlatformContext();
        Services.AddSingleton<IPlatformContext>(fake);
        Services.AddTreeGraphPlatform();

        var ctx = Services.GetRequiredService<IPlatformContext>();
        Assert.Same(fake, ctx);
    }

    [Fact]
    public async Task RefreshAsync_JsUnavailable_SilentlyReturns()
    {
        var ssrJs = new SsrJsRuntime(); // 抛 InvalidOperationException 模拟 SSR
        var ctx = new WebPlatformContext(ssrJs);

        await ctx.RefreshAsync(); // 不应抛出

        Assert.Equal(PlatformFormFactor.Web, ctx.FormFactor);
        Assert.False(ctx.IsMobile);
        Assert.False(ctx.IsCompact);
    }

    [Fact]
    public async Task RefreshAsync_Width375_IsMobile()
    {
        JSInterop.Setup<int>("eval", "window.innerWidth").SetResult(375);
        var ctx = new WebPlatformContext(JSInterop.JSRuntime);

        await ctx.RefreshAsync();

        Assert.Equal(PlatformFormFactor.Phone, ctx.FormFactor);
        Assert.True(ctx.IsMobile);
        Assert.True(ctx.IsCompact);
    }

    [Fact]
    public async Task RefreshAsync_Width800_IsCompactNotMobile()
    {
        JSInterop.Setup<int>("eval", "window.innerWidth").SetResult(800);
        var ctx = new WebPlatformContext(JSInterop.JSRuntime);

        await ctx.RefreshAsync();

        Assert.Equal(PlatformFormFactor.Tablet, ctx.FormFactor);
        Assert.False(ctx.IsMobile);
        Assert.True(ctx.IsCompact);
    }

    [Fact]
    public async Task RefreshAsync_Width1280_NotMobileNotCompact()
    {
        JSInterop.Setup<int>("eval", "window.innerWidth").SetResult(1280);
        var ctx = new WebPlatformContext(JSInterop.JSRuntime);

        await ctx.RefreshAsync();

        Assert.Equal(PlatformFormFactor.Desktop, ctx.FormFactor);
        Assert.False(ctx.IsMobile);
        Assert.False(ctx.IsCompact);
    }

    [Fact]
    public async Task RefreshAsync_CrossBreakpoint_RaisesChangedOnce()
    {
        var setup = JSInterop.Setup<int>("eval", "window.innerWidth");
        setup.SetResult(375);
        var ctx = new WebPlatformContext(JSInterop.JSRuntime);

        var changedCount = 0;
        ctx.Changed += () => changedCount++;

        await ctx.RefreshAsync(); // Web -> Phone
        Assert.Equal(1, changedCount);

        setup.SetResult(800);
        await ctx.RefreshAsync(); // Phone -> Tablet
        Assert.Equal(2, changedCount);
    }

    [Fact]
    public async Task RefreshAsync_SameFormFactor_NoChangedEvent()
    {
        var setup = JSInterop.Setup<int>("eval", "window.innerWidth");
        setup.SetResult(1280);
        var ctx = new WebPlatformContext(JSInterop.JSRuntime);

        var changedCount = 0;
        ctx.Changed += () => changedCount++;

        await ctx.RefreshAsync(); // Web -> Desktop
        Assert.Equal(1, changedCount);

        setup.SetResult(1024); // Desktop -> Desktop（同形态）
        await ctx.RefreshAsync();
        Assert.Equal(1, changedCount);
    }

    /// <summary>模拟 SSR 阶段 JS 不可用。</summary>
    private sealed class SsrJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new InvalidOperationException("JS interop not available during SSR.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new InvalidOperationException("JS interop not available during SSR.");
    }
}
