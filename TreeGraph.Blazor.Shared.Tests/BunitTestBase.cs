using Xunit;

namespace TreeGraph.Blazor.Shared.Tests;

/// <summary>
/// bUnit 测试基类：解决 BunitContext + MudBlazor 9 的 Dispose 兼容问题。
/// BunitContext 实现了 IAsyncDisposable，但 xUnit 2.x 只调用 IDisposable.Dispose()，
/// 导致 MudBlazor.PointerEventsNoneService 的同步 Dispose 抛 InvalidOperationException。
/// 通过 IAsyncLifetime 让 xUnit 走异步清理路径。
/// </summary>
public abstract class BunitTestBase : Bunit.BunitContext, IAsyncLifetime
{
    public virtual Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}
