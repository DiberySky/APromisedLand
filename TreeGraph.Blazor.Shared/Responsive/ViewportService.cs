using Microsoft.JSInterop;

namespace TreeGraph.Blazor.Shared.Responsive;

/// <summary>
/// 视口宽度服务。
///
/// - 提供 IsMobile、Width
/// - 视口变化时触发 OnChanged
/// - Scoped 生命周期（与 Circuit 对齐）
/// </summary>
public class ViewportService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ViewportService>? _ref;
    private bool _initialized;
    private int _width = 1024;

    public ViewportService(IJSRuntime js) => _js = js;

    public int Width => _width;

    /// <summary>断点由 ResponsiveOptions 决定，默认 768。</summary>
    public bool IsMobile => _width < ResponsiveOptions.MobileBreakpoint;

    public event Action? OnChanged;

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        try
        {
            _width = await _js.InvokeAsync<int>("responsive.getWidth");
            _ref = DotNetObjectReference.Create(this);
            await _js.InvokeVoidAsync("responsive.registerResize", _ref);
        }
        catch
        {
            // SSR / JS 不可用：保持默认值
        }
        finally
        {
            _initialized = true;
        }
    }

    [JSInvokable]
    public void OnResize(int width)
    {
        if (_width == width) return;
        _width = width;
        OnChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            try { await _js.InvokeVoidAsync("responsive._unregisterResize"); }
            catch { /* 忽略 */ }
        }
        _ref?.Dispose();
    }
}
