namespace TreeGraph.Blazor.Shared.Platform;

/// <summary>平台形态因子。Web 宿主下按视口宽度映射（600/960 断点）。</summary>
public enum PlatformFormFactor { Unknown, Phone, Tablet, Desktop, Web }

/// <summary>
/// 平台上下文抽象：让 RCL 组件感知宿主平台与设备形态。
/// 全部显式抽象，不使用接口默认实现，避免与覆盖实现冲突。
/// </summary>
public interface IPlatformContext
{
    /// <summary>当前设备形态。</summary>
    PlatformFormFactor FormFactor { get; }

    /// <summary>是否为手机形态（视口 &lt; 600px，或 MAUI Phone Idiom）。</summary>
    bool IsMobile { get; }

    /// <summary>是否为紧凑布局（手机或平板：视口 &lt; 960px）。</summary>
    bool IsCompact { get; }

    /// <summary>是否以触控为主要输入方式。</summary>
    bool IsTouchPrimary { get; }

    /// <summary>宿主类型标识："Server" | "Hybrid" | "WebAssembly"。</summary>
    string HostKind { get; }

    /// <summary>视口/形态发生变化时触发（如 window.resize 越过断点）。</summary>
    event Action? Changed;

    /// <summary>
    /// 刷新平台信息。Web 实现通过 JS interop 读取 window.innerWidth；
    /// 首次渲染后由组件或宿主调用一次。SSR 阶段 JS 不可用时静默返回。
    /// </summary>
    Task RefreshAsync();
}
