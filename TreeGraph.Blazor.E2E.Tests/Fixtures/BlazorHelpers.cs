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
