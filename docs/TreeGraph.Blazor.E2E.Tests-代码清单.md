# TreeGraph.Blazor.E2E.Tests C# 代码清单

- 生成时间：2026-10-04 21:37:24
- 文件总数：12
- 排除：bin/、obj/、csproj、README.md
- 项目状态：Playwright E2E 24 项（iNode/元数据 21 + TreeSkyDemoSmoke 3）

## 文件 1/12 TreeGraph.Blazor.E2E.Tests/Base/E2ETestBase.cs

```csharp
using Microsoft.Playwright;
using System.Net.Http.Json;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Base;

[Collection("E2E")]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly PlaywrightFixture Fixture;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    protected E2ETestBase(PlaywrightFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        Context = await Fixture.NewContextAsync();

        // CI 中录制 Playwright Trace（失败时可在线回放）
        var isCi = Environment.GetEnvironmentVariable("CI") == "true";
        if (isCi)
        {
            await Context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true,
            });
        }

        Page = await Context.NewPageAsync();
        Page.SetDefaultTimeout(Fixture.Settings.DefaultTimeoutMs);
    }

    public async Task DisposeAsync()
    {
        try
        {
            // ★ CI 下保存 trace 到文件（文件名含测试类名，便于定位）
            if (Environment.GetEnvironmentVariable("CI") == "true")
            {
                Directory.CreateDirectory("test-results/traces");
                var path = $"test-results/traces/" +
                           $"{GetType().Name}-{Guid.NewGuid():N}.zip";

                await Context.Tracing.StopAsync(new TracingStopOptions
                {
                    Path = path
                });
            }
        }
        catch
        {
            // trace 保存失败不阻断测试清理
        }

        if (Page is not null) await Page.CloseAsync();
        if (Context is not null) await Context.DisposeAsync();
    }

    /// <summary>生成唯一 iNode ID（避免测试间干扰）。</summary>
    protected static string NewInodeId() => Guid.NewGuid().ToString("D");

    /// <summary>
    /// 通过 API 建实体类型（供 E2E 前置准备）。
    /// 直接调 API 而不是 UI，避免"类型管理"页面成为 E2E 的前置依赖。
    /// 不指定 entityType → 服务端自动生成 et_xxx 标识并返回。
    /// </summary>
    protected async Task<(string Id, string Name)> CreateEntityTypeWithNameAsync(
        string displayName)
    {
        using var http = new HttpClient { BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl) };

        var resp = await http.PostAsJsonAsync("/api/eav/entity-types", new
        {
            displayName
        });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<CreateTypeResponse>();
        return (result!.EntityTypeId, result.EntityType);
    }

    /// <summary>
    /// 通过 API 建属性（供 E2E 前置准备）。
    /// </summary>
    protected async Task CreateAttributeViaApiAsync(
        string entityType, string attributeName, string displayName,
        string dataType = "string",
        bool isSearchable = true)
    {
        using var http = new HttpClient { BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl) };

        var resp = await http.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType,
            attributeName,
            displayName,
            dataType,
            isSearchable,
            displayOrder = 1
        });
        resp.EnsureSuccessStatusCode();
    }

    private record CreateTypeResponse(string EntityTypeId, string EntityType);
}
```

## 文件 2/12 TreeGraph.Blazor.E2E.Tests/Fixtures/AspireHealthCheck.cs

```csharp
namespace TreeGraph.Blazor.E2E.Tests.Fixtures;

/// <summary>
/// 轮询等待 Aspire 编排的服务就绪。
/// 由外部 `dotnet run --project APromisedLand.AppHost` 提供。
/// </summary>
public static class AspireHealthCheck
{
    /// <summary>
    /// 等待 Blazor 前端可访问。
    /// 命中 200 或任何 HTTP 响应（含 302/401）即视为就绪。
    /// </summary>
    public static async Task WaitForBlazorAsync(
        string baseUrl, int timeoutSeconds, CancellationToken ct = default)
    {
        await WaitForHttpAsync(baseUrl + "/", timeoutSeconds, ct);
    }

    /// <summary>
    /// 等待 EAV API 就绪（走 /health 端点）。
    /// </summary>
    public static async Task WaitForApiAsync(
        string baseUrl, int timeoutSeconds, CancellationToken ct = default)
    {
        await WaitForHttpAsync(baseUrl + "/health", timeoutSeconds, ct);
    }

    private static async Task WaitForHttpAsync(
        string url, int timeoutSeconds, CancellationToken ct)
    {
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        Exception? lastEx = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var resp = await http.GetAsync(url, ct);
                // 任何响应（含 4xx）都说明服务已在监听
                if (resp.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable)
                    return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }

            await Task.Delay(1000, ct);
        }

        throw new TimeoutException(
            $"服务 {url} 在 {timeoutSeconds}s 内未就绪。" +
            $"请确认 Aspire AppHost 已启动。最后异常: {lastEx?.Message}");
    }
}
```

## 文件 3/12 TreeGraph.Blazor.E2E.Tests/Fixtures/BlazorHelpers.cs

```csharp
using Microsoft.Playwright;

namespace TreeGraph.Blazor.E2E.Tests.Fixtures;

/// <summary>
/// Blazor Server + MudBlazor 专用等待/交互辅助。
///
/// 设计原则：
///   1. 每个操作前先确认 SignalR 连接健康
///   2. 断言显式声明期望（Snackbar 文本、导航 URL）
///   3. 涉及 Blazor Server 特性的操作都带重试
///
/// 详细说明见 BlazorHelpers.README.md。
/// </summary>
public static class BlazorHelpers
{
    // ============================================================
    // Blazor 就绪
    // ============================================================

    /// <summary>等待 window.Blazor 对象出现（SignalR 客户端已加载）。</summary>
    public static async Task WaitForBlazorAsync(
        IPage page, int timeoutMs = 15000)
    {
        await page.WaitForFunctionAsync(
            "() => window.Blazor !== undefined",
            null,
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    /// <summary>
    /// ★ 等待 SignalR 连接健康（无重连弹窗）。
    ///
    /// Blazor Server 通过 `&lt;dialog id="components-reconnect-modal"&gt;` 显示重连状态：
    ///   - dialog.open == false → 连接正常
    ///   - dialog.open == true  → 正在重连
    ///
    /// 使用场景：任何可能触发导航或写操作的点击前先调用，避免"点了没反应"。
    /// </summary>
    public static async Task WaitForSignalRConnectedAsync(
        IPage page, int timeoutMs = 10000)
    {
        await page.WaitForFunctionAsync(
            @"() => {
                if (!window.Blazor) return false;
                const modal = document.getElementById('components-reconnect-modal');
                if (!modal) return true;
                return !modal.open;
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    /// <summary>
    /// 探测当前 SignalR 连接状态（不等待）。
    /// 用于 debug / 断言场景（等待用 WaitForSignalRConnectedAsync）。
    /// </summary>
    public static async Task<bool> IsSignalRConnectedAsync(IPage page)
    {
        return await page.EvaluateAsync<bool>(
            @"() => {
                if (!window.Blazor) return false;
                const modal = document.getElementById('components-reconnect-modal');
                return !modal || !modal.open;
            }");
    }

    // ============================================================
    // 导航
    // ============================================================

    /// <summary>
    /// 直接导航到指定 URL（用 GoTo，不走 Blazor 路由）。
    /// </summary>
    public static async Task GoToAsync(
        IPage page, string relativeUrl, int timeoutMs = 15000)
    {
        await page.GotoAsync(relativeUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = timeoutMs
        });
        await WaitForBlazorAsync(page, timeoutMs);
    }

    // ============================================================
    // ★ 核心：带重试的点击 + 导航
    // ============================================================

    /// <summary>
    /// ★ 点击会触发导航的按钮（Blazor Server 专用，带重试）。
    ///
    /// 为什么需要重试：
    ///   Blazor Server 通过 SignalR 保持长连接。连接可能在以下时机断开：
    ///     · 页面导航时旧 Circuit 释放、新 Circuit 建立
    ///     · Aspire 服务热重载
    ///     · SignalR 心跳超时
    ///   断开期间 Playwright 的 ClickAsync 会返回成功（DOM 层），
    ///   但服务端从未收到点击 → 静默失败。
    ///
    /// 重试策略：
    ///   每次尝试前先 WaitForSignalRConnectedAsync；
    ///   点击 + 导航等待并行注册；
    ///   单次尝试失败后等待 500ms 再重试。
    ///
    /// 参数：
    ///   maxAttempts          - 最多尝试次数（默认 4）
    ///   perAttemptTimeoutMs  - 单次尝试的超时（默认 6s）
    ///                         总耗时上限 ≈ maxAttempts × perAttemptTimeoutMs
    /// </summary>
    public static async Task ClickAndNavigateAsync(
        IPage page,
        ILocator button,
        string expectedUrlPattern,
        int maxAttempts = 4,
        int perAttemptTimeoutMs = 6000)
    {
        Exception? lastError = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var navTask = page.WaitForURLAsync(
                expectedUrlPattern,
                new PageWaitForURLOptions { Timeout = perAttemptTimeoutMs });

            try
            {
                // 1. 确认连接健康（否则点击丢失）
                await WaitForSignalRConnectedAsync(page);

                // 2. 点击（导航等待已在上面注册）
                await button.ClickAsync(new LocatorClickOptions
                {
                    Timeout = perAttemptTimeoutMs
                });

                // 3. 等导航完成
                await navTask;

                // 4. 等新页面 Blazor 就绪
                await WaitForBlazorAsync(page);

                return;   // 成功退出
            }
            catch (TimeoutException ex)
            {
                lastError = ex;
                // 消化已注册的等待任务，避免未观察异常
                try { await navTask; } catch { /* 已处理，吞掉 */ }
            }
            catch (PlaywrightException ex)
            {
                lastError = ex;
                try { await navTask; } catch { /* 已处理，吞掉 */ }
            }

            // 失败：等一小段再重试（给 SignalR 重连留时间）
            if (attempt < maxAttempts)
            {
                await Task.Delay(500);
            }
        }

        throw new TimeoutException(
            $"ClickAndNavigateAsync 在 {maxAttempts} 次尝试后失败。" +
            $"期望 URL 模式: {expectedUrlPattern}。" +
            $"当前 URL: {page.Url}。" +
            $"最后错误: {lastError?.Message}",
            lastError);
    }

    /// <summary>
    /// 点击不触发导航的按钮（如"保存"），但仍可能因 SignalR 断线而丢失。
    /// 只做一次尝试，调用方通过后续断言（Snackbar / DOM 变化）判断是否成功。
    ///
    /// 若"点击后无响应"是已知问题，用 ClickAndNavigateAsync 或自行加重试。
    /// </summary>
    public static async Task ClickAsync(
        IPage page, ILocator button, int timeoutMs = 6000)
    {
        await WaitForSignalRConnectedAsync(page);
        await button.ClickAsync(new LocatorClickOptions { Timeout = timeoutMs });
    }

    /// <summary>
    /// 用唯一文本锚定 MudDrawer。
    ///
    /// ★ MudBlazor Temporary 抽屉关闭后保留 DOM，页面上可能有多个 .mud-drawer，
    ///   .First 会按 DOM 顺序命中不含内容的隐藏残留节点。
    ///   用标题文本（恒渲染、唯一）锚定真抽屉。
    /// </summary>
    public static ILocator FindDrawerByTitle(IPage page, string title)
    {
        return page.Locator(".mud-drawer")
            .Filter(new LocatorFilterOptions { HasText = title })
            .First;
    }

    // ============================================================
    // Snackbar
    // ============================================================

    /// <summary>
    /// 等待 Snackbar 出现。
    ///
    /// expectedText:
    ///   - null：任意 Snackbar 出现即返回（不推荐，会抓到旧的）
    ///   - 非 null：必须包含该文本才返回（推荐）
    ///
    /// 实现：轮询所有可见 Snackbar，文本匹配则立即返回。
    /// </summary>
    public static async Task<string> WaitForSnackbarAsync(
        IPage page,
        string? expectedText = null,
        int timeoutMs = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var locator = page.Locator(".mud-snackbar-content-message");

        while (DateTime.UtcNow < deadline)
        {
            var count = await locator.CountAsync();
            for (int i = 0; i < count; i++)
            {
                var item = locator.Nth(i);
                try
                {
                    if (!await item.IsVisibleAsync()) continue;
                    var text = await item.InnerTextAsync();
                    if (expectedText is null || text.Contains(expectedText))
                        return text;
                }
                catch
                {
                    // 元素可能已消失，忽略
                }
            }
            await Task.Delay(100);
        }

        var actual = await GetAllSnackbarTextsAsync(page);
        throw new TimeoutException(
            $"等待 Snackbar 超时（{timeoutMs}ms）。" +
            $"期望包含: '{expectedText ?? "(任意)"}'，" +
            $"实际可见: [{string.Join(" | ", actual)}]");
    }

    /// <summary>
    /// 等待所有 Snackbar 消失。
    ///
    /// 使用场景：进入下一步操作前，先清理干净当前 Snackbar。
    /// 超时后强制移除（防御性，正常情况下不应触发）。
    /// </summary>
    public static async Task WaitForSnackbarGoneAsync(
        IPage page, int timeoutMs = 8000)
    {
        var locator = page.Locator(".mud-snackbar");
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            var count = await locator.CountAsync();
            if (count == 0) return;

            var anyVisible = false;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    if (await locator.Nth(i).IsVisibleAsync())
                    {
                        anyVisible = true;
                        break;
                    }
                }
                catch { }
            }

            if (!anyVisible) return;
            await Task.Delay(100);
        }

        // 超时兜底
        await page.EvaluateAsync(
            "() => document.querySelectorAll('.mud-snackbar').forEach(e => e.remove())");
    }

    /// <summary>
    /// 关闭当前 Snackbar（点击区域本身，MudBlazor 支持）。
    /// 关闭后等所有 Snackbar 消失。
    /// </summary>
    public static async Task DismissSnackbarAsync(IPage page)
    {
        var snackbars = page.Locator(".mud-snackbar");
        var count = await snackbars.CountAsync();

        for (int i = count - 1; i >= 0; i--)
        {
            try
            {
                var item = snackbars.Nth(i);
                if (await item.IsVisibleAsync())
                    await item.ClickAsync(new LocatorClickOptions
                    {
                        Force = true,
                        Timeout = 2000
                    });
            }
            catch
            {
                // 点击失败忽略，最后由 WaitForSnackbarGoneAsync 兜底
            }
        }

        await WaitForSnackbarGoneAsync(page);
    }

    private static async Task<List<string>> GetAllSnackbarTextsAsync(IPage page)
    {
        var result = new List<string>();
        var locator = page.Locator(".mud-snackbar-content-message");
        var count = await locator.CountAsync();
        for (int i = 0; i < count; i++)
        {
            try { result.Add(await locator.Nth(i).InnerTextAsync()); }
            catch { }
        }
        return result;
    }

    // ============================================================
    // Dialog
    // ============================================================

    public static async Task<ILocator> WaitForDialogAsync(
        IPage page, int timeoutMs = 10000)
    {
        var dialog = page.Locator(".mud-dialog");
        await dialog.First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
        return dialog.First;
    }

    // ============================================================
    // 表单
    // ============================================================

    /// <summary>
    /// 在 MudTextField 中填值。
    /// MudBlazor 的 input 需要 Blur 才会触发 ValueChanged。
    /// </summary>
    public static async Task FillMudTextFieldAsync(
        ILocator fieldLocator, string value)
    {
        var input = fieldLocator.Locator("input, textarea").First;
        await input.FillAsync(value);
        await input.BlurAsync();
    }

    public static async Task WaitForLoadingCompleteAsync(
        IPage page, int timeoutMs = 15000)
    {
        var progressBar = page.Locator(".mud-progress-linear").First;
        try
        {
            await progressBar.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException) { /* 页面可能无进度条 */ }
    }

    // ============================================================
    // ExpansionPanel
    // ============================================================

    /// <summary>
    /// 展开 MudExpansionPanel。
    ///
    /// ★ MudBlazor 9.9 实测：
    ///   - MudExpansionPanel **不渲染** aria-expanded 属性
    ///   - 展开状态通过以下 DOM 信号表达：
    ///       1. 内容 wrapper 出现 .mud-collapse-entered
    ///       2. 图标 class 多出 mud-transform
    ///   本方法用多信号探测（任一命中即为已展开）。
    ///
    /// 已展开时直接返回（幂等）。
    /// </summary>
    public static async Task ExpandPanelAsync(
        IPage page, ILocator panel, int timeoutMs = 6000)
    {
        if (await IsPanelExpandedAsync(panel))
            return;

        var header = panel.Locator(MudSelectors.ExpandPanelHeader).First;
        await ClickAsync(page, header);

        // 等展开完成（多信号任一命中）
        await panel.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = timeoutMs
        });

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await IsPanelExpandedAsync(panel)) return;
            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"ExpandPanelAsync 超时 {timeoutMs}ms，面板未展开。");
    }

    /// <summary>
    /// 多信号探测面板是否已展开（任一命中即为真）。
    /// 顺序：mud-collapse-entered → mud-transform 图标 → 内容可见
    /// </summary>
    private static async Task<bool> IsPanelExpandedAsync(ILocator panel)
    {
        try
        {
            // 信号 1：内容 wrapper 有 mud-collapse-entered
            if (await panel.Locator(MudSelectors.CollapseEntered).CountAsync() > 0)
                return true;

            // 信号 2：图标带 mud-transform
            var transformedIcon = panel.Locator(
                $"{MudSelectors.ExpandPanelIcon}{MudSelectors.TransformIcon}");
            if (await transformedIcon.CountAsync() > 0)
                return true;
        }
        catch { }

        return false;
    }
}

/// <summary>
/// MudBlazor CSS 选择器集中管理。
/// MudBlazor 升级改类名时只改这里。
///
/// 类名来源：MudBlazor 9.9 实际渲染（可用 DevTools 核对）。
/// 注意：ExpansionPanel 的类名是 expand（无 sion），与组件名 MudExpansionPanel 拼写不同。
/// </summary>
public static class MudSelectors
{
    // ExpansionPanel
    public const string ExpandPanel = ".mud-expand-panel";
    public const string ExpandPanelHeader = ".mud-expand-panel-header";
    public const string ExpandPanelIcon = ".mud-expand-panel-icon";
    public const string CollapseEntered = ".mud-collapse-entered";
    public const string TransformIcon = ".mud-transform";

    // Dialog / Snackbar
    public const string Dialog = ".mud-dialog";
    public const string Snackbar = ".mud-snackbar";
    public const string SnackbarMessage = ".mud-snackbar-content-message";

    // Table / Select / List
    public const string TableRow = ".mud-table-row";
    public const string Select = ".mud-select";
    public const string ListItem = ".mud-list-item";

    // Form
    public const string InputControl = ".mud-input-control";
    public const string ProgressLinear = ".mud-progress-linear";
}
```

## 文件 4/12 TreeGraph.Blazor.E2E.Tests/Fixtures/E2ESettings.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Blazor.E2E.Tests.Fixtures;

public class E2ESettings
{
    public string BlazorBaseUrl { get; init; } = "http://localhost:5783";
    public string ApiBaseUrl { get; init; } = "http://localhost:5773";
    public int HealthProbeTimeoutSeconds { get; init; } = 120;
    public int DefaultTimeoutMs { get; init; } = 15000;

    /// <summary>
    /// 读取 appsettings.json（可选），环境变量 E2E_ 前缀覆盖。
    /// 不引入 Microsoft.Extensions.Configuration，保持测试项目依赖最小。
    /// </summary>
    public static E2ESettings Load()
    {
        string? blazor = null, api = null;
        int? health = null, timeout = null;

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("E2E", out var s))
                {
                    if (s.TryGetProperty("BlazorBaseUrl", out var v) && v.ValueKind == JsonValueKind.String)
                        blazor = v.GetString();
                    if (s.TryGetProperty("ApiBaseUrl", out var v2) && v2.ValueKind == JsonValueKind.String)
                        api = v2.GetString();
                    if (s.TryGetProperty("HealthProbeTimeoutSeconds", out var v3) && v3.TryGetInt32(out var h))
                        health = h;
                    if (s.TryGetProperty("DefaultTimeoutMs", out var v4) && v4.TryGetInt32(out var t))
                        timeout = t;
                }
            }
            catch (JsonException)
            {
                // 配置文件损坏时退回默认值
            }
        }

        return new E2ESettings
        {
            BlazorBaseUrl = Environment.GetEnvironmentVariable("E2E_BlazorBaseUrl") ?? blazor ?? "http://localhost:5783",
            ApiBaseUrl = Environment.GetEnvironmentVariable("E2E_ApiBaseUrl") ?? api ?? "http://localhost:5773",
            HealthProbeTimeoutSeconds =
                int.TryParse(Environment.GetEnvironmentVariable("E2E_HealthProbeTimeoutSeconds"), out var eh)
                    ? eh : health ?? 120,
            DefaultTimeoutMs =
                int.TryParse(Environment.GetEnvironmentVariable("E2E_DefaultTimeoutMs"), out var et)
                    ? et : timeout ?? 15000
        };
    }
}
```

## 文件 5/12 TreeGraph.Blazor.E2E.Tests/Fixtures/MetadataHelpers.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.E2E.Tests.Fixtures;

/// <summary>
/// 元数据页面 E2E 的公共辅助。
/// </summary>
public static class MetadataHelpers
{
    /// <summary>生成唯一名称（用于 DisplayName / Name）。</summary>
    public static string Unique(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"{prefix}_{suffix}";
    }

    /// <summary>
    /// 通过 API 创建实体类型。返回 (EntityTypeId, EntityType, DisplayName)。
    /// DisplayName 必须唯一 —— 用 Unique() 生成。
    /// </summary>
    public static async Task<(string Id, string Name, string DisplayName)>
        CreateEntityTypeAsync(HttpClient http, string displayName)
    {
        var resp = await http.PostAsJsonAsync("/api/eav/entity-types",
            new { displayName });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadFromJsonAsync<CreateTypeResponse>();
        return (result!.EntityTypeId, result.EntityType, displayName);
    }

    /// <summary>删除实体类型（尽力而为，失败忽略）。</summary>
    public static async Task TryDeleteEntityTypeAsync(
        HttpClient http, string entityTypeId)
    {
        try
        {
            await http.DeleteAsync($"/api/eav/entity-types/{entityTypeId}");
        }
        catch { /* 忽略 */ }
    }

    /// <summary>
    /// 按 DisplayName 反查并删除实体类型（兜底清理）。
    /// 用于 UI 创建的测试（拿不到 ID），失败忽略不阻断测试。
    /// </summary>
    public static async Task TryDeleteEntityTypeByNameAsync(
        HttpClient http, string displayName)
    {
        try
        {
            var list = await http.GetFromJsonAsync<List<EntityTypeDetailDto>>(
                "/api/eav/entity-types/details");
            var target = list?.FirstOrDefault(t => t.DisplayName == displayName);
            if (target is not null)
                await http.DeleteAsync($"/api/eav/entity-types/{target.EntityTypeId}");
        }
        catch { /* 忽略 */ }
    }

    /// <summary>通过 API 创建属性。</summary>
    public static async Task CreateAttributeAsync(
        HttpClient http, string entityType, string attributeName,
        string displayName, string dataType = "string")
    {
        var resp = await http.PostAsJsonAsync("/api/eav/metadata/attributes",
            new
            {
                entityType,
                attributeName,
                displayName,
                dataType,
                isSearchable = true,
                displayOrder = 1
            });
        resp.EnsureSuccessStatusCode();
    }

    // ============================================================
    // OptionSet
    // ============================================================

    /// <summary>
    /// 按 SetName 反查并删除选项集（兜底清理）。
    ///
    /// 行为：
    ///   - 若选项集被属性引用，DeleteSet 会拒绝（400），此时静默跳过
    ///   - 若不存在，静默跳过
    ///   - 任何异常都不抛出，避免影响测试主体
    ///
    /// 用于 E2E 每个场景的 finally 块，防止 DB 累积膨胀。
    /// </summary>
    public static async Task TryDeleteOptionSetByNameAsync(
        HttpClient http, string setName)
    {
        try
        {
            var list = await http.GetFromJsonAsync<List<OptionSetSummaryDto>>(
                "/api/eav/metadata/option-sets");
            var target = list?.FirstOrDefault(s => s.SetName == setName);
            if (target is null) return;

            // DeleteSet 会级联软删 items（后端实现）
            await http.DeleteAsync(
                $"/api/eav/metadata/option-sets/{target.OptionSetId}");
        }
        catch { /* 忽略 */ }
    }

    /// <summary>
    /// 按 OptionSetId 删除选项集（用于已知 ID 的场景）。
    /// </summary>
    public static async Task TryDeleteOptionSetAsync(
        HttpClient http, string optionSetId)
    {
        try
        {
            await http.DeleteAsync(
                $"/api/eav/metadata/option-sets/{optionSetId}");
        }
        catch { /* 忽略 */ }
    }

    private sealed record CreateTypeResponse(
        string EntityTypeId, string EntityType);
}
```

## 文件 6/12 TreeGraph.Blazor.E2E.Tests/Fixtures/PlaywrightFixture.cs

```csharp
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
```

## 文件 7/12 TreeGraph.Blazor.E2E.Tests/Tests/InodeFlowTests.cs

```csharp
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// iNode 完整用户流程 E2E。
///
/// 每个测试用独立的 entityType + 独立 iNodeId，互不干扰。
/// 前置条件：Aspire AppHost 已启动（Blazor 5783 / API 5773）。
/// </summary>
public class InodeFlowTests : E2ETestBase
{
    public InodeFlowTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：从列表页进入空 iNode，声明类型
    // ============================================================

    [Fact]
    public async Task Flow_NewInode_DeclareType_ShowsCard()
    {
        // 前置：建类型
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E商品_{Guid.NewGuid():N}");

        // 1. 打开 /inodes
        await BlazorHelpers.GoToAsync(Page, "/inodes");
        await Page.WaitForSelectorAsync("h5:has-text('iNode 列表')");

        // 2. 输入 iNode GUID → 点"访问"
        var inodeId = NewInodeId();
        var input = Page.Locator(
            "input[placeholder='xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx']").First;
        await input.FillAsync(inodeId);

        var accessBtn = Page.Locator("button:has-text('访问')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, accessBtn, $"**/inodes/{inodeId}");

        // 3. 跳到详情页
        await Page.WaitForSelectorAsync("h5:has-text('iNode 详情')");

        // 4. 空状态：显示"声明新类型"按钮
        await Page.WaitForSelectorAsync("text=该 iNode 尚未声明任何类型");

        // 5. 点"声明新类型"
        var declareBtn = Page.Locator("button:has-text('声明新类型')").First;
        await declareBtn.ClickAsync();

        // 6. 对话框里选类型
        var dialog = await BlazorHelpers.WaitForDialogAsync(Page);
        var select = dialog.Locator(".mud-select").First;
        await select.ClickAsync();

        // 7. 选中类型（按 displayName 匹配）
        var option = Page.Locator($".mud-list-item:has-text('{typeName}')").First;
        await option.ClickAsync();

        // 8. 点"声明"
        var confirmBtn = dialog.Locator("button:has-text('声明')").Last;
        await confirmBtn.ClickAsync();

        // 9. 校验 Snackbar（传期望文本，避免抓到旧 Snackbar）
        var snackbar = await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        Assert.Contains("声明成功", snackbar);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 10. 卡片出现
        await Page.WaitForSelectorAsync($".mud-card:has-text('{typeName}')");
        await Page.WaitForSelectorAsync("text=未填写");
    }

    // ============================================================
    // 场景 2：填写实体 → 刷新后数据持久化
    // ============================================================

    [Fact]
    public async Task Flow_FillEntity_Reload_Persists()
    {
        // 前置：建类型 + 建 string / int 属性
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E订单_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(
            typeName, "order_no", "订单号", "string");
        await CreateAttributeViaApiAsync(
            typeName, "amount", "金额", "int");

        var inodeId = NewInodeId();

        // 1. 直接访问详情页 + 声明 + 填写
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");

        // ★ 关键操作前确认连接正常
        Assert.True(await BlazorHelpers.IsSignalRConnectedAsync(Page),
            "SignalR 应处于连接状态");

        // 声明
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dialog = await BlazorHelpers.WaitForDialogAsync(Page);
        await dialog.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dialog.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 2. 点"填写"进入编辑页
        var card = Page.Locator($".mud-card:has-text('{typeName}')").First;
        await BlazorHelpers.ClickAndNavigateAsync(
            Page, card.Locator("button:has-text('填写')"),
            $"**/inodes/{inodeId}/types/**");

        // 3. 填表单
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // string 属性 → MudTextField
        var orderNoField = Page.Locator(".mud-input-control:has-text('订单号')").First;
        await BlazorHelpers.FillMudTextFieldAsync(orderNoField, "SO-001");

        // int 属性 → MudNumericField（同样是 input 元素）
        var amountField = Page.Locator(".mud-input-control:has-text('金额')").First;
        await BlazorHelpers.FillMudTextFieldAsync(amountField, "999");

        // 4. 点保存
        var saveBtn = Page.Locator("button:has-text('保存')").First;
        await saveBtn.ClickAsync();

        var snackbar = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", snackbar);

        // 5. 刷新页面
        await Page.ReloadAsync();
        await BlazorHelpers.WaitForBlazorAsync(Page);
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // 6. 校验表单值持久化
        var orderNoAfterReload = Page.Locator(".mud-input-control:has-text('订单号')")
            .Locator("input").First;
        var orderNoValue = await orderNoAfterReload.InputValueAsync();
        Assert.Equal("SO-001", orderNoValue);

        var amountAfterReload = Page.Locator(".mud-input-control:has-text('金额')")
            .Locator("input").First;
        var amountValue = await amountAfterReload.InputValueAsync();
        Assert.Equal("999", amountValue);
    }

    // ============================================================
    // 场景 3：编辑已有实体 → iNode 详情页显示"已填写"
    // ============================================================

    [Fact]
    public async Task Flow_AfterSave_DetailShowsFilled()
    {
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E库存_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(
            typeName, "warehouse", "仓库", "string");

        var inodeId = NewInodeId();

        // 声明 + 填写
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await dlg.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dlg.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        await BlazorHelpers.ClickAndNavigateAsync(
            Page,
            Page.Locator($".mud-card:has-text('{typeName}') button:has-text('填写')").First,
            $"**/inodes/{inodeId}/types/**");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        var whField = Page.Locator(".mud-input-control:has-text('仓库')").First;
        await BlazorHelpers.FillMudTextFieldAsync(whField, "BJ-01");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var savedSnack = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", savedSnack);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 返回 iNode 详情
        var backBtn = Page.Locator("button:has-text('返回 iNode')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, backBtn, $"**/inodes/{inodeId}");

        // 校验"已填写"
        var card = Page.Locator($".mud-card:has-text('{typeName}')").First;
        await card.Locator("text=已填写").WaitForAsync();
    }

    // ============================================================
    // 场景 4：审计历史显示
    // ============================================================

    [Fact]
    public async Task Flow_History_ShowsChanges()
    {
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E审计_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(
            typeName, "note", "备注", "string");

        var inodeId = NewInodeId();

        // 快速填写 2 次
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await dlg.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dlg.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        await BlazorHelpers.ClickAndNavigateAsync(
            Page,
            Page.Locator($".mud-card:has-text('{typeName}') button:has-text('填写')").First,
            $"**/inodes/{inodeId}/types/**");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // 第一次保存
        var noteField = Page.Locator(".mud-input-control:has-text('备注')").First;
        await BlazorHelpers.FillMudTextFieldAsync(noteField, "v1");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var saved1 = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", saved1);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 第二次保存
        await BlazorHelpers.FillMudTextFieldAsync(noteField, "v2");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var saved2 = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", saved2);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 返回 iNode 详情，点"历史"
        var backBtn = Page.Locator("button:has-text('返回 iNode')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, backBtn, $"**/inodes/{inodeId}");

        var histBtn = Page.Locator(
            $".mud-card:has-text('{typeName}') button:has-text('历史')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, histBtn, "**/history");
        await Page.WaitForSelectorAsync("h5:has-text('审计历史')");

        // 至少有一条时间线项
        var timelineItems = Page.Locator(".mud-timeline-item");
        await timelineItems.First.WaitForAsync();
        var count = await timelineItems.CountAsync();
        Assert.True(count >= 2, $"期望 >= 2 条历史，实际 {count}");

        // 包含 v1 / v2
        var pageText = await Page.ContentAsync();
        Assert.Contains("v1", pageText);
        Assert.Contains("v2", pageText);
    }

    // ============================================================
    // 场景 5：删除实体
    // ============================================================

    [Fact]
    public async Task Flow_DeleteEntity_BackToUnfilled()
    {
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E删除_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(
            typeName, "x", "X", "string");

        var inodeId = NewInodeId();

        // 填写
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await dlg.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dlg.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        await BlazorHelpers.ClickAndNavigateAsync(
            Page,
            Page.Locator($".mud-card:has-text('{typeName}') button:has-text('填写')").First,
            $"**/inodes/{inodeId}/types/**");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        var xField = Page.Locator(".mud-input-control:has-text('X')").First;
        await BlazorHelpers.FillMudTextFieldAsync(xField, "value");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var saved = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", saved);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 删除
        var deleteBtn = Page.Locator("button:has-text('删除实体')").First;
        await deleteBtn.ClickAsync();

        // 确认对话框
        var confirmDlg = await BlazorHelpers.WaitForDialogAsync(Page);
        var yesBtn = confirmDlg.Locator("button:has-text('永久删除')").First;
        await BlazorHelpers.ClickAndNavigateAsync(
            Page, yesBtn, $"**/inodes/{inodeId}");

        // 卡片显示"未填写"
        var card = Page.Locator($".mud-card:has-text('{typeName}')").First;
        await card.Locator("text=未填写").WaitForAsync();
    }

    // ============================================================
    // 场景 6：JSON 预览抽屉
    //
    // 只保护：
    //   - 抽屉能打开（UI 入口没坏）
    //   - JSON 契约（含 entities / 类型名 / 我们填的值）
    //   - includeNull 开关生效（保护"ValueChanged 忽略参数"类回归）
    //   - 关闭按钮工作
    //
    // 不测：
    //   - displayName / originalUnits（与 includeNull 同模式，重复覆盖）
    //   - 复制按钮（浏览器剪贴板权限在 CI 环境不稳定）
    //   - JSON 格式化细节（脆弱，断言我们控得住的字段）
    // ============================================================

    [Fact]
    public async Task Flow_JsonPreviewDrawer_Works()
    {
        // 前置：建类型 + 2 个 string 属性。
        // ★ 必须只填 note、留 extra 不填——否则 includeNull=true 时
        //   没有未写入属性可补 null，"等 null 出现"永远超时。
        var (_, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2EJSON_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(typeName, "note", "备注", "string");
        await CreateAttributeViaApiAsync(typeName, "extra", "附加", "string");

        var inodeId = NewInodeId();

        // 声明 + 填写（确保 JSON 里有数据）
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await dlg.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dlg.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        await BlazorHelpers.ClickAndNavigateAsync(
            Page,
            Page.Locator($".mud-card:has-text('{typeName}') button:has-text('填写')").First,
            $"**/inodes/{inodeId}/types/**");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        var noteField = Page.Locator(".mud-input-control:has-text('备注')").First;
        await BlazorHelpers.FillMudTextFieldAsync(noteField, "hello-json");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var saved = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", saved);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 回到 iNode 详情
        var backBtn = Page.Locator("button:has-text('返回 iNode')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, backBtn, $"**/inodes/{inodeId}");

        // ---- 1. 打开抽屉 ----
        await BlazorHelpers.ClickAsync(Page,
            Page.Locator("button:has-text('JSON 预览')").First);

        // ★ 用标题锚定真抽屉（MudBlazor Temporary 抽屉会残留隐藏 DOM，
        //   .First 可能命中残留节点）
        var drawer = BlazorHelpers.FindDrawerByTitle(Page, "原始 JSON 预览");
        await drawer.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        // ---- 2. 等 JSON 渲染完成（pre 非空）----
        // ★ querySelector 只取第一个匹配（可能命中残留 drawer），
        //   必须用 querySelectorAll + some()
        await Page.WaitForFunctionAsync(
            @"() => {
                const pres = document.querySelectorAll('.mud-drawer pre');
                return Array.from(pres).some(p => p.textContent.length > 10);
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var textDefault = await drawer.Locator("pre").First.InnerTextAsync();

        // ---- 3. 断言关键契约（只断言我们控得住的）----
        Assert.Contains("entities", textDefault);
        Assert.Contains(typeName, textDefault);
        Assert.Contains("hello-json", textDefault);

        // 默认（includeNull=false）只含已写属性，不应出现 null
        Assert.DoesNotContain("null", textDefault);

        // ---- 4. 切 includeNull → 未填的 extra 以 null 出现 ----
        await drawer.Locator(".mud-switch:has-text('includeNull')")
            .Locator("input").First.ClickAsync();

        await Page.WaitForFunctionAsync(
            "() => document.querySelector('.mud-drawer pre')" +
            "?.textContent?.includes('null')",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // ---- 5. 关闭抽屉 ----
        await drawer.Locator("button[title='关闭']").First.ClickAsync();

        // ★ MudBlazor 9.9 关闭的 Temporary drawer 用 transform 移出屏幕，
        //   元素无 display:none → Playwright 判定仍"可见"，
        //   WaitForAsync(Hidden) 永远超时。关闭信号 = class 含
        //   mud-drawer--closed（双横线）；元素被移除（!d）也算关闭。
        await Page.WaitForFunctionAsync(
            @"() => {
                const d = Array.from(document.querySelectorAll('.mud-drawer'))
                    .find(x => x.textContent.includes('原始 JSON 预览'));
                return !d || d.className.includes('mud-drawer--closed');
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });
    }
}
```

## 文件 8/12 TreeGraph.Blazor.E2E.Tests/Tests/MetadataAttributesTests.cs

```csharp
using System.Net.Http.Json;
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /metadata/attributes 页面 E2E。
/// </summary>
public class MetadataAttributesTests : E2ETestBase
{
    public MetadataAttributesTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：选择实体类型 → 显示属性列表
    // ============================================================

    [Fact]
    public async Task SelectEntityType_ShowsAttributes()
    {
        // 前置：建类型 + 1 个属性
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E属性页"));
        var attrDisplay = MetadataHelpers.Unique("屏幕");

        try
        {
            await MetadataHelpers.CreateAttributeAsync(
                http, name, "screen_size", attrDisplay, "decimal");

            await BlazorHelpers.GoToAsync(Page, "/metadata/attributes");
            await Page.WaitForSelectorAsync("h5:has-text('属性定义管理')");

            // 等下拉加载
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            // 打开实体类型下拉
            var typeSelect = Page.Locator(".mud-select").First;
            await BlazorHelpers.ClickAsync(Page, typeSelect);

            // 选中我们建的类型
            var option = Page.Locator(
                $".mud-list-item:has-text('{displayName}')").First;
            await option.WaitForAsync();
            await option.ClickAsync();

            // 表格出现该属性
            await Page.WaitForSelectorAsync(
                $".mud-table-row:has-text('{attrDisplay}')");
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }

    // ============================================================
    // 场景 2：新建属性（不展开高级选项，AttributeName 自动生成）
    // ============================================================

    [Fact]
    public async Task Create_OnlyDisplayName_AutoGeneratesName()
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E新属性"));

        try
        {
            // 用一个已有属性占位，让页面能加载（不然该类型无属性，页面显示空）
            await MetadataHelpers.CreateAttributeAsync(
                http, name, "seed", "占位", "string");

            await BlazorHelpers.GoToAsync(Page, "/metadata/attributes");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            // 选类型
            var typeSelect = Page.Locator(".mud-select").First;
            await BlazorHelpers.ClickAsync(Page, typeSelect);
            await Page.Locator($".mud-list-item:has-text('{displayName}')")
                .First.ClickAsync();

            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            // 点"新建属性"
            await BlazorHelpers.ClickAsync(Page,
                Page.Locator("button:has-text('新建属性')").First);

            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            // 显示名（不展开高级选项）
            var attrDisplay = MetadataHelpers.Unique("自动命名");
            var nameField = dlg.Locator(
                ".mud-input-control:has-text('显示名')").First;
            await BlazorHelpers.FillMudTextFieldAsync(nameField, attrDisplay);

            // 保存
            var saveBtn = dlg.Locator("button:has-text('创建')").Last;
            await BlazorHelpers.ClickAsync(Page, saveBtn);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 表格出现
            await Page.WaitForSelectorAsync(
                $".mud-table-row:has-text('{attrDisplay}')");

            // 副行包含 attr_ 前缀
            var row = Page.Locator(
                $".mud-table-row:has-text('{attrDisplay}')").First;
            var rowText = await row.InnerTextAsync();
            Assert.Contains("attr_", rowText);
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }

    // ============================================================
    // 场景 3：新建属性（展开高级选项，显式指定 AttributeName）
    // ============================================================

    [Fact]
    public async Task Create_WithExplicitName_UsesGiven()
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E显式名"));

        try
        {
            await MetadataHelpers.CreateAttributeAsync(
                http, name, "seed", "占位", "string");

            await BlazorHelpers.GoToAsync(Page, "/metadata/attributes");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page, Page.Locator(".mud-select").First);
            await Page.Locator($".mud-list-item:has-text('{displayName}')")
                .First.ClickAsync();
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page,
                Page.Locator("button:has-text('新建属性')").First);
            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            // 显示名
            var attrDisplay = MetadataHelpers.Unique("显式命名");
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('显示名')").First,
                attrDisplay);

            // 展开"高级选项（内部标识）"
            var advanced = dlg.Locator(
                $"{MudSelectors.ExpandPanel}:has-text('高级选项')").First;
            await BlazorHelpers.ExpandPanelAsync(Page, advanced);

            // 填内部标识
            var explicitName = $"my_field_{Guid.NewGuid():N}"[..20];
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('内部标识')").First,
                explicitName);

            // 保存
            await BlazorHelpers.ClickAsync(Page,
                dlg.Locator("button:has-text('创建')").Last);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 副行含显式名
            var row = Page.Locator(
                $".mud-table-row:has-text('{attrDisplay}')").First;
            var rowText = await row.InnerTextAsync();
            Assert.Contains(explicitName, rowText);
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }

    // ============================================================
    // 场景 4：非法内部标识 → 前端拦截
    // ============================================================

    [Fact]
    public async Task Create_InvalidExplicitName_ShowsWarning()
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E非法名"));

        try
        {
            await MetadataHelpers.CreateAttributeAsync(
                http, name, "seed", "占位", "string");

            await BlazorHelpers.GoToAsync(Page, "/metadata/attributes");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page, Page.Locator(".mud-select").First);
            await Page.Locator($".mud-list-item:has-text('{displayName}')")
                .First.ClickAsync();
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page,
                Page.Locator("button:has-text('新建属性')").First);
            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('显示名')").First,
                MetadataHelpers.Unique("测试"));

            // 展开高级选项
            await BlazorHelpers.ExpandPanelAsync(Page,
                dlg.Locator($"{MudSelectors.ExpandPanel}:has-text('高级选项')").First);

            // 填非法名（数字开头）
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('内部标识')").First,
                "123_invalid");

            // 保存
            await BlazorHelpers.ClickAsync(Page,
                dlg.Locator("button:has-text('创建')").Last);

            // 前端拦截：出现 Warning Snackbar
            var snackbar = await BlazorHelpers.WaitForSnackbarAsync(Page,
                expectedText: "内部标识");
            Assert.Contains("内部标识", snackbar);

            // 对话框仍未关闭（因为校验失败）
            Assert.True(await dlg.IsVisibleAsync());
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }
}
```

## 文件 9/12 TreeGraph.Blazor.E2E.Tests/Tests/MetadataEntityTypesTests.cs

```csharp
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /metadata/entity-types 页面 E2E。
/// </summary>
public class MetadataEntityTypesTests : E2ETestBase
{
    public MetadataEntityTypesTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：页面加载 + 表格渲染
    // ============================================================

    [Fact]
    public async Task Page_Loads_ShowsTable()
    {
        await BlazorHelpers.GoToAsync(Page, "/metadata/entity-types");

        await Page.WaitForSelectorAsync("h5:has-text('实体类型管理')");
        await Page.WaitForSelectorAsync("button:has-text('新建实体类型')");

        // 至少 EavSeeder 的 Product 在
        await Page.WaitForSelectorAsync(".mud-table");
    }

    // ============================================================
    // 场景 2：新建实体类型（只填 DisplayName）
    // ============================================================

    [Fact]
    public async Task Create_OnlyDisplayName_Succeeds()
    {
        var displayName = MetadataHelpers.Unique("E2E类型");
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };

        try
        {
            await BlazorHelpers.GoToAsync(Page, "/metadata/entity-types");

            // 点"新建实体类型"
            await BlazorHelpers.ClickAsync(Page,
                Page.Locator("button:has-text('新建实体类型')").First);

            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            // 只填名称，不填内部标识（新方案已隐藏）
            var nameField = dlg.Locator(".mud-input-control:has-text('实体类型名称')").First;
            await BlazorHelpers.FillMudTextFieldAsync(nameField, displayName);

            // 点"保存"
            var saveBtn = dlg.Locator("button:has-text('保存')").Last;
            await saveBtn.ClickAsync();

            var snackbar = await BlazorHelpers.WaitForSnackbarAsync(Page, "已创建");
            Assert.Contains(displayName, snackbar);

            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 表格出现该名称
            // ★ LoadAsync 现走批量详情接口（1 次 HTTP），仍给 30s 兜底防网络抖动。
            await Page.WaitForSelectorAsync(
                $".mud-table-row:has-text('{displayName}')",
                new PageWaitForSelectorOptions { Timeout = 30000 });
        }
        finally
        {
            // ★ 自清理：按 DisplayName 反查删除，防止 DB 类型数无限膨胀
            await MetadataHelpers.TryDeleteEntityTypeByNameAsync(http, displayName);
        }
    }

    // ============================================================
    // 场景 3：编辑显示名
    // ============================================================

    [Fact]
    public async Task Edit_DisplayName_UpdatesRow()
    {
        // 前置：API 建类型
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E编辑前"));
        var newDisplayName = MetadataHelpers.Unique("E2E编辑后");

        try
        {
            await BlazorHelpers.GoToAsync(Page, "/metadata/entity-types");

            // 找目标行
            var row = Page.Locator($".mud-table-row:has-text('{displayName}')").First;
            await row.WaitForAsync();

            // 点编辑图标
            var editBtn = row.Locator("button[title='编辑']").First;
            await BlazorHelpers.ClickAsync(Page, editBtn);

            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
            var nameField = dlg.Locator(".mud-input-control:has-text('实体类型名称')").First;
            await BlazorHelpers.FillMudTextFieldAsync(nameField, newDisplayName);

            var saveBtn = dlg.Locator("button:has-text('保存')").Last;
            await saveBtn.ClickAsync();

            await BlazorHelpers.WaitForSnackbarAsync(Page, "更新成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 表格更新
            await Page.WaitForSelectorAsync(
                $".mud-table-row:has-text('{newDisplayName}')");
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }

    // ============================================================
    // 场景 4：删除（确认对话框）
    // ============================================================

    [Fact]
    public async Task Delete_RemovesRow()
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (_, _, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E删除"));

        await BlazorHelpers.GoToAsync(Page, "/metadata/entity-types");

        var row = Page.Locator($".mud-table-row:has-text('{displayName}')").First;
        await row.WaitForAsync();

        var delBtn = row.Locator("button[title='删除']").First;
        await BlazorHelpers.ClickAsync(Page, delBtn);

        // MudBlazor MessageBox 是 dialog
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        var confirmBtn = dlg.Locator("button:has-text('删除')").Last;
        await confirmBtn.ClickAsync();

        await BlazorHelpers.WaitForSnackbarAsync(Page, "删除成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 表格不再有该行
        var remaining = Page.Locator($".mud-table-row:has-text('{displayName}')");
        Assert.Equal(0, await remaining.CountAsync());
    }
}
```

## 文件 10/12 TreeGraph.Blazor.E2E.Tests/Tests/MetadataOptionSetsTests.cs

```csharp
using System.Net.Http.Json;
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /metadata/option-sets 页面 E2E。
/// 注意：MudBlazor 9 展开面板 header 类名是 .mud-expand-panel-header。
/// </summary>
public class MetadataOptionSetsTests : E2ETestBase
{
    public MetadataOptionSetsTests(PlaywrightFixture fixture) : base(fixture) { }

    private HttpClient NewHttp() => new()
    {
        BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
    };

    // ============================================================
    // 场景 1：页面加载 + 显示种子选项集
    // ============================================================

    [Fact]
    public async Task Page_Loads_ShowsSeededSets()
    {
        await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
        await Page.WaitForSelectorAsync("h5:has-text('选项集管理')");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // EavSeeder 建了 gender / quality_grade
        await Page.WaitForSelectorAsync("text=gender");
        await Page.WaitForSelectorAsync("text=quality_grade");
    }

    // ============================================================
    // 场景 2：新建选项集
    // ============================================================

    [Fact]
    public async Task Create_NewSet_Succeeds()
    {
        var setName = MetadataHelpers.Unique("e2e_set");
        var displayName = MetadataHelpers.Unique("E2E选项集");
        using var http = NewHttp();

        try
        {
            await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page,
                Page.Locator("button:has-text('新建选项集')").First);

            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            // 实体类型（默认 Shared，不用改）
            // 集合名
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('集合名')").First,
                setName);
            // 显示名
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('显示名')").First,
                displayName);

            await BlazorHelpers.ClickAsync(Page,
                dlg.Locator("button:has-text('创建')").Last);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 展开面板出现
            await Page.WaitForSelectorAsync(
                $"{MudSelectors.ExpandPanel}:has-text('{displayName}')");
        }
        finally
        {
            // ★ 兜底清理（UI 建的，拿不到 ID，按 name 反查）
            await MetadataHelpers.TryDeleteOptionSetByNameAsync(http, setName);
        }
    }

    // ============================================================
    // 场景 3：添加选项
    // ============================================================

    [Fact]
    public async Task AddItem_ToNewSet_ShowsInList()
    {
        // 前置：API 建选项集
        using var http = NewHttp();
        var setName = MetadataHelpers.Unique("e2e_items");

        string? setId = null;
        try
        {
            var createResp = await http.PostAsJsonAsync(
                "/api/eav/metadata/option-sets",
                new { entityType = "Shared", setName, displayName = setName });
            createResp.EnsureSuccessStatusCode();

            var created = await createResp.Content
                .ReadFromJsonAsync<IdResponse>();
            setId = created!.OptionSetId;

            await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            // 展开我们建的那个面板
            var panel = Page.Locator(
                $"{MudSelectors.ExpandPanel}:has-text('{setName}')").First;
            await BlazorHelpers.ExpandPanelAsync(Page, panel);

            // 点"添加选项"
            await BlazorHelpers.ClickAsync(Page,
                panel.Locator("button:has-text('添加选项')").First);

            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('Value')").First,
                "e2e_val");
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('Label')").First,
                "E2E标签");

            await BlazorHelpers.ClickAsync(Page,
                dlg.Locator("button:has-text('保存')").Last);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "添加成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 行出现
            await panel.Locator(".mud-table-row:has-text('e2e_val')")
                .First.WaitForAsync();
        }
        finally
        {
            // ★ 兜底清理（API 建的，有 ID）
            if (setId is not null)
                await MetadataHelpers.TryDeleteOptionSetAsync(http, setId);
        }
    }

    // ============================================================
    // 场景 4：设为默认
    // ============================================================

    [Fact]
    public async Task SetDefault_ShowsStarIcon()
    {
        // 前置：API 建选项集 + 2 个选项
        using var http = NewHttp();
        var setName = MetadataHelpers.Unique("e2e_default");

        string? setId = null;
        try
        {
            var createResp = await http.PostAsJsonAsync(
                "/api/eav/metadata/option-sets",
                new { entityType = "Shared", setName, displayName = setName });
            createResp.EnsureSuccessStatusCode();

            var created = await createResp.Content
                .ReadFromJsonAsync<IdResponse>();
            setId = created!.OptionSetId;

            foreach (var v in new[] { "a", "b" })
            {
                await http.PostAsJsonAsync(
                    $"/api/eav/metadata/option-sets/{setId}/items",
                    new { value = v, label = v.ToUpper(), displayOrder = 0, isDefault = false });
            }

            await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            var panel = Page.Locator(
                $"{MudSelectors.ExpandPanel}:has-text('{setName}')").First;
            await BlazorHelpers.ExpandPanelAsync(Page, panel);

            // 等表格出现（限定 tbody，避免 has-text('a') 匹配到含 "Value" 文本的表头行）
            var rowA = panel.Locator(".mud-table-body .mud-table-row:has-text('a')").First;
            await rowA.WaitForAsync();

            // 点"设为默认"（星号）
            await BlazorHelpers.ClickAsync(Page,
                rowA.Locator("button[title='设为默认']").First);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "设为默认");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 校验 a 行有实心星
            await rowA.Locator(".mud-icon-root").First.WaitForAsync();
        }
        finally
        {
            // ★ 兜底清理（API 建的，有 ID）
            if (setId is not null)
                await MetadataHelpers.TryDeleteOptionSetAsync(http, setId);
        }
    }

    private sealed record IdResponse(string OptionSetId);
}
```

## 文件 11/12 TreeGraph.Blazor.E2E.Tests/Tests/MetadataUnitsTests.cs

```csharp
using System.Net.Http.Json;
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /metadata/units 页面 E2E。
/// 注意：分类名（如 length / weight）显示在分类标题 h6 里，
/// 不在表格内 —— 分类断言用 h6，单位名断言用表格行。
/// </summary>
public class MetadataUnitsTests : E2ETestBase
{
    public MetadataUnitsTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：页面加载 + 显示分类
    // ============================================================

    [Fact]
    public async Task Page_Loads_ShowsCategories()
    {
        await BlazorHelpers.GoToAsync(Page, "/metadata/units");
        await Page.WaitForSelectorAsync("h5:has-text('单位管理')");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // Seed 数据里至少有 length / weight（分类名在 h6 标题）
        await Page.WaitForSelectorAsync("h6:has-text('length')");
        await Page.WaitForSelectorAsync("h6:has-text('weight')");
    }

    // ============================================================
    // 场景 2：新建单位
    // ============================================================

    [Fact]
    public async Task Create_NewUnit_Succeeds()
    {
        var uniqueCat = MetadataHelpers.Unique("e2e_cat");
        var uniqueName = MetadataHelpers.Unique("E2E单位");

        await BlazorHelpers.GoToAsync(Page, "/metadata/units");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        await BlazorHelpers.ClickAsync(Page,
            Page.Locator("button:has-text('新建单位')").First);

        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

        // 分类
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('分类')").First,
            uniqueCat);
        // 名称
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('名称')").First,
            uniqueName);
        // 符号
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('符号')").First,
            "EU");
        // 换算系数
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('换算系数')").First,
            "1");

        // 保存
        await BlazorHelpers.ClickAsync(Page,
            dlg.Locator("button:has-text('创建')").Last);

        await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 新分类出现（h6 标题）+ 单位行出现
        await Page.WaitForSelectorAsync($"h6:has-text('{uniqueCat}')");
        await Page.WaitForSelectorAsync(
            $".mud-table-row:has-text('{uniqueName}')");
    }

    // ============================================================
    // 场景 3：编辑单位名称
    // ============================================================

    [Fact]
    public async Task Edit_UnitName_UpdatesRow()
    {
        // 前置：API 建单位（直接用 UI 建太慢，且需要分类已有）
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var uniqueCat = MetadataHelpers.Unique("e2e_edit");
        var uniqueName = MetadataHelpers.Unique("E2E原");
        var newName = MetadataHelpers.Unique("E2E新");

        // 建单位
        var createResp = await http.PostAsJsonAsync("/api/units", new
        {
            category = uniqueCat,
            name = uniqueName,
            symbol = "EU",
            toBaseFactor = 1m,
            isBaseUnit = true,
            displayOrder = 1
        });
        createResp.EnsureSuccessStatusCode();

        await BlazorHelpers.GoToAsync(Page, "/metadata/units");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // 等分类出现（h6 标题）
        await Page.WaitForSelectorAsync($"h6:has-text('{uniqueCat}')");

        var row = Page.Locator($".mud-table-row:has-text('{uniqueName}')").First;
        await row.WaitForAsync();

        // 点编辑
        await BlazorHelpers.ClickAsync(Page,
            row.Locator("button[title='编辑']").First);

        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('名称')").First,
            newName);

        await BlazorHelpers.ClickAsync(Page,
            dlg.Locator("button:has-text('保存')").Last);

        await BlazorHelpers.WaitForSnackbarAsync(Page, "更新成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 表更新
        await Page.WaitForSelectorAsync(
            $".mud-table-row:has-text('{newName}')");
    }
}
```

## 文件 12/12 TreeGraph.Blazor.E2E.Tests/Tests/TreeSkyDemoSmokeTests.cs

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /tree-sky-demo 页面的 smoke E2E —— TreeSky 通用树组件的回归保护。
///
/// 演示页就是裸 TreeSky&lt;StringTreeNode&gt;，因此这 3 个场景同时覆盖：
///   1. 初始根节点渲染 + MudTreeView ServerData 懒加载（Items 与 ServerData 共存）
///   2. 全对话框链路创建子节点 → 成功提示 → 树局部刷新出新节点
///      然后同节点走"节点操作"对话框 + 确认框删除（创建/删除两条写链路）
///   3. API 前置 + UI 编辑（对话框链路 + ITreeActionHandler.UpdateNodeAsync）
///
/// DOM 事实（2026-10-04 对 MudBlazor 9.11 实测）：
///   - 行内容容器 .mud-treeview-item-content 只含本行内容，子节点在祖先 li 下的独立 li 中
///   - 行内 MoreHoriz 按钮 class 含 mud-icon-button-size-small
///   - 展开按钮在同行 li 下 .mud-treeview-item-arrow 内、class 含 mud-treeview-item-expand-button
///   - 创建/编辑对话框提交按钮（DialogSky）为 Text 变体 Success 色，class 含
///     mud-button-text-success；用 class 锚定以与操作对话框的其他 Text 按钮区分
///   - 操作对话框里还有一个 label 为"名称"的只读字段；编辑表单的名称 label 是"名称（string）"
/// </summary>
public class TreeSkyDemoSmokeTests : E2ETestBase
{
    private const string RootName = "物品总类";
    private const string FurnitureName = "家具（空分类）";
    private const string ElectronicsName = "电子产品";

    public TreeSkyDemoSmokeTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：页面加载 + 种子根节点渲染 + 懒加载展开
    // ============================================================

    [Fact]
    public async Task Page_Loads_SeededTree_AndLazyExpandsChildren()
    {
        await BlazorHelpers.GoToAsync(Page, "/tree-sky-demo");

        await Page.WaitForSelectorAsync("h4:has-text('TreeSky 树组件演示')");
        await Row(RootName).First.WaitForAsync(Visible());

        // 初始只有根，子节点懒加载
        Assert.Equal(0, await Row(ElectronicsName).CountAsync());

        await ExpandAsync(RootName, ElectronicsName);

        // 展开后三个一级分类都在
        await Row("电子产品").First.WaitForAsync(Visible());
        await Row("办公用品").First.WaitForAsync(Visible());
        await Row(FurnitureName).First.WaitForAsync(Visible());
    }

    // ============================================================
    // 场景 2：UI 创建子节点 → 出现 + 成功提示；随后 UI 删除 → 消失 + 成功提示
    // ============================================================

    [Fact]
    public async Task CreateChild_ThenDelete_FullDialogChain_Works()
    {
        var uniqueName = MetadataHelpers.Unique("e2e_树节点");

        await BlazorHelpers.GoToAsync(Page, "/tree-sky-demo");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);
        await Row(RootName).First.WaitForAsync(Visible());
        await ExpandAsync(RootName, FurnitureName);

        // 打开"家具（空分类）"的节点操作对话框 → 创建子项
        await OpenActionsAsync(FurnitureName);
        var actionsDlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await BlazorHelpers.ClickAsync(Page,
            actionsDlg.Locator("button:has-text('创建子项')").First);

        // 创建对话框：名称（string）为必填
        var nameField = Page.Locator(
            ".mud-dialog .mud-input-control:has-text('名称（string）')").First;
        await nameField.WaitForAsync(Visible());
        await BlazorHelpers.FillMudTextFieldAsync(nameField, uniqueName);

        // 提交按钮无文字，用其实测 class 锚定（仅 SaveAs 图标）
        await BlazorHelpers.ClickAsync(Page, SubmitButton());
        await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 父节点自动展开、局部刷新后新节点出现在树中
        await Row(uniqueName).First.WaitForAsync(Visible());

        // 同节点：节点操作 → 删除 → 确认框
        await OpenActionsAsync(uniqueName);
        actionsDlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await BlazorHelpers.ClickAsync(Page,
            actionsDlg.Locator("button:has-text('删除')").First);

        var confirm = Page.Locator(".mud-dialog")
            .Filter(new LocatorFilterOptions { HasText = "确认删除" }).First;
        await confirm.WaitForAsync(Visible());
        await BlazorHelpers.ClickAsync(Page,
            confirm.Locator("button:has-text('删除')").First);

        await BlazorHelpers.WaitForSnackbarAsync(Page, "删除成功");

        // 节点从树中消失
        await AssertNodeGoneAsync(uniqueName);
    }

    // ============================================================
    // 场景 3：API 前置建叶节点 → UI 编辑改名 → 成功提示 + 树中文本更新
    // ============================================================

    [Fact]
    public async Task EditNode_ThroughDialog_UpdatesTreeText()
    {
        var oldName = MetadataHelpers.Unique("e2e_编辑前");
        var newName = MetadataHelpers.Unique("e2e_编辑后");

        using var api = NewApiClient();
        var nodeId = await CreateLeafViaApiAsync(api, oldName, "cat-furniture");

        try
        {
            await BlazorHelpers.GoToAsync(Page, "/tree-sky-demo");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);
            await Row(RootName).First.WaitForAsync(Visible());
            await ExpandAsync(RootName, FurnitureName);
            await ExpandAsync(FurnitureName, oldName);

            await OpenActionsAsync(oldName);
            var actionsDlg = await BlazorHelpers.WaitForDialogAsync(Page);
            await BlazorHelpers.ClickAsync(Page,
                actionsDlg.Locator("button:has-text('设置修改')").First);

            var nameField = Page.Locator(
                ".mud-dialog .mud-input-control:has-text('名称（string）')").First;
            await nameField.WaitForAsync(Visible());
            await BlazorHelpers.FillMudTextFieldAsync(nameField, newName);

            await BlazorHelpers.ClickAsync(Page, SubmitButton());
            await BlazorHelpers.WaitForSnackbarAsync(Page, "更新成功");

            await Row(newName).First.WaitForAsync(Visible());
            Assert.Equal(0, await Row(oldName).CountAsync());
        }
        finally
        {
            await TryDeleteViaApiAsync(api, nodeId);
        }
    }

    // ============================================================
    // 辅助
    // ============================================================

    /// <summary>
    /// 锚定某个节点"本行"的内容容器。MudBlazor 把子节点渲染在独立 li 中，
    /// 因此 .mud-treeview-item-content 内只含本行文本，不会被子孙文本污染。
    /// </summary>
    private ILocator Row(string text) =>
        Page.Locator(".mud-treeview-item-content",
            new PageLocatorOptions { HasText = text });

    private ILocator SubmitButton() =>
        Page.Locator(".mud-dialog button.mud-button-text-success").First;

    private static LocatorWaitForOptions Visible() =>
        new() { State = WaitForSelectorState.Visible, Timeout = 15000 };

    /// <summary>
    /// 确保某节点已展开并可见指定子节点（幂等：子节点已在 DOM 则跳过点击）。
    /// </summary>
    private async Task ExpandAsync(string parentText, string childText)
    {
        if (await Row(childText).CountAsync() > 0) return;

        // 调用时子节点尚未加载，li 内只有本行的展开按钮，不会误命中嵌套行
        var arrow = Row(parentText).First
            .Locator("xpath=ancestor::li[1]//button[contains(@class,'mud-treeview-item-expand-button')]")
            .First;
        await BlazorHelpers.ClickAsync(Page, arrow);
        await Row(childText).First.WaitForAsync(Visible());
    }

    /// <summary>点击节点行右侧的 MoreHoriz（三个点）图标按钮。</summary>
    private async Task OpenActionsAsync(string nodeText)
    {
        await BlazorHelpers.ClickAsync(Page,
            Row(nodeText).First.Locator("button.mud-icon-button-size-small").First);
    }

    private async Task AssertNodeGoneAsync(string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await Row(text).CountAsync() == 0) return;
            await Task.Delay(150);
        }
        Assert.Equal(0, await Row(text).CountAsync());
    }

    private HttpClient NewApiClient() =>
        new() { BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl) };

    /// <summary>
    /// 通过真实 API 在指定父节点下建叶节点（UI 编辑场景的前置准备）。
    /// 返回服务端生成的节点 ID。
    /// </summary>
    private async Task<string> CreateLeafViaApiAsync(
        HttpClient api, string name, string parentId)
    {
        var resp = await api.PostAsJsonAsync("/StringTreeNode", new
        {
            text = name,
            parentId,
            value = new { name }
        });
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync());
        var data = doc.RootElement.GetProperty("data");
        return data.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()!
            : data.GetProperty("value").GetProperty("id").GetString()!;
    }

    /// <summary>按 ID 删除节点（测试清理，失败不掩盖主体断言）。</summary>
    private static async Task TryDeleteViaApiAsync(HttpClient api, string nodeId)
    {
        try { await api.DeleteAsync($"/StringTreeNode/{nodeId}"); }
        catch { /* 尽力清理 */ }
    }
}
```

