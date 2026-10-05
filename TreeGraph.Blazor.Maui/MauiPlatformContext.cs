using TreeGraph.Blazor.Shared.Platform;

namespace TreeGraph.Blazor.Maui;

/// <summary>
/// MAUI 平台上下文：用 DeviceDisplay.MainDisplayInfo 替代 JS window.innerWidth，
/// 用 DeviceDisplay.MainDisplayInfoChanged 替代 resize 事件。
/// </summary>
public class MauiPlatformContext : IPlatformContext
{
    private PlatformFormFactor _formFactor = PlatformFormFactor.Unknown;

    public MauiPlatformContext()
    {
        Refresh();
        DeviceDisplay.MainDisplayInfoChanged += OnMainDisplayInfoChanged;
    }

    public PlatformFormFactor FormFactor => _formFactor;
    public bool IsMobile => _formFactor == PlatformFormFactor.Phone;
    public bool IsCompact => _formFactor is PlatformFormFactor.Phone or PlatformFormFactor.Tablet;
    public bool IsTouchPrimary => IsCompact;
    public string HostKind => "Hybrid";

    public event Action? Changed;

    public Task RefreshAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        var info = DeviceDisplay.MainDisplayInfo;
        // MAUI: Width/Height 以设备无关像素 (dp) 为单位
        // 手机: 宽度 < 600dp；平板: 600-960dp；桌面: >= 960dp
        var widthDp = info.Width / info.Density;

        var next = widthDp < 600 ? PlatformFormFactor.Phone
                 : widthDp < 960 ? PlatformFormFactor.Tablet
                 : PlatformFormFactor.Desktop;

        if (next != _formFactor)
        {
            _formFactor = next;
            Changed?.Invoke();
        }
    }

    private void OnMainDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        Refresh();
    }
}
