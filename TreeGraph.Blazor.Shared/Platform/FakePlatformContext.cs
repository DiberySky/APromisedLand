namespace TreeGraph.Blazor.Shared.Platform;

/// <summary>测试用平台上下文：属性可直接赋值，RaiseChanged 手动触发事件。</summary>
public class FakePlatformContext : IPlatformContext
{
    public PlatformFormFactor FormFactor { get; set; } = PlatformFormFactor.Desktop;
    public bool IsMobile { get; set; }
    public bool IsCompact { get; set; }
    public bool IsTouchPrimary { get; set; }
    public string HostKind { get; set; } = "Test";
    public event Action? Changed;
    public int RefreshCallCount { get; private set; }

    public Task RefreshAsync() { RefreshCallCount++; return Task.CompletedTask; }
    public void RaiseChanged() => Changed?.Invoke();
}
