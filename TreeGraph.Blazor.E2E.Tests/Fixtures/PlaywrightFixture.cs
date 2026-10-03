using Microsoft.Playwright;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Fixtures;

/// <summary>
/// Playwright 生命周期管理（全局共享）。
///
/// 由 xUnit 的 CollectionFixture 注入，所有测试类共享同一个浏览器实例。
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public E2ESettings Settings { get; } = E2ESettings.Load();

    // IAsyncLifetime

    public async Task InitializeAsync()
    {
        // 1. 等待 Aspire 编排就绪
        await AspireHealthCheck.WaitForApiAsync(
            Settings.ApiBaseUrl, Settings.HealthProbeTimeoutSeconds);
        await AspireHealthCheck.WaitForBlazorAsync(
            Settings.BlazorBaseUrl, Settings.HealthProbeTimeoutSeconds);

        // 2. 启动 Playwright
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();
    }

    // 便捷方法

    public async Task<IBrowserContext> NewContextAsync()
    {
        // CI 环境录制视频（失败排查用；本地默认关闭，避免磁盘占用）
        var isCi = Environment.GetEnvironmentVariable("CI") == "true";

        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Settings.BlazorBaseUrl,
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            RecordVideoDir = isCi ? "test-results/videos/" : null,
            RecordVideoSize = isCi
                ? new RecordVideoSize { Width = 1280, Height = 720 }
                : null,
        });
        return context;
    }
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<PlaywrightFixture> { }
