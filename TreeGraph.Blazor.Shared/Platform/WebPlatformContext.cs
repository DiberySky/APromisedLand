using Microsoft.JSInterop;

namespace TreeGraph.Blazor.Shared.Platform;

/// <summary>
/// Blazor Server 平台上下文：HostKind=Server，设备形态通过 JS 读取 window.innerWidth 判定。
/// 初始（SSR/尚未 RefreshAsync）按桌面处理，避免首屏误触发布局；
/// SSR 首帧桌面→JS 检测后可能切换移动端，存在一帧闪烁，属已接受行为。
/// </summary>
public class WebPlatformContext : IPlatformContext
{
    private readonly IJSRuntime _js;
    private PlatformFormFactor _formFactor = PlatformFormFactor.Web;

    public WebPlatformContext(IJSRuntime js) => _js = js;

    public PlatformFormFactor FormFactor => _formFactor;

    // Web 宿主下形态由视口宽度决定；SSR 首帧（_formFactor=Web）视为桌面
    public bool IsMobile => _formFactor == PlatformFormFactor.Phone;
    public bool IsCompact => _formFactor is PlatformFormFactor.Phone or PlatformFormFactor.Tablet;
    public bool IsTouchPrimary => IsCompact;
    public string HostKind => "Server";

    public event Action? Changed;

    /// <summary>读取 window.innerWidth 并按 MudBlazor 断点（600/960）映射形态。</summary>
    public async Task RefreshAsync()
    {
        int width;
        try
        {
            width = await _js.InvokeAsync<int>("eval", "window.innerWidth");
        }
        catch (InvalidOperationException)
        {
            // SSR 阶段 JS 不可用，保持当前形态
            return;
        }

        var next = width < 600 ? PlatformFormFactor.Phone
                 : width < 960 ? PlatformFormFactor.Tablet
                 : PlatformFormFactor.Desktop;

        if (next != _formFactor)
        {
            _formFactor = next;
            Changed?.Invoke();
        }
    }
}
