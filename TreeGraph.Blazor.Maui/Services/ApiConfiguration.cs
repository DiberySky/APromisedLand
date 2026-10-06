namespace TreeGraph.Blazor.Maui.Services;

/// <summary>
/// 按平台/设备形态解析 API Base URL。
///
/// 背景：
///   - Windows / MacCatalyst / iOS 模拟器：宿主与 API 在同一机，localhost 可达。
///   - Android 模拟器：localhost 指向模拟器自身；10.0.2.2 由模拟器 NAT 映射到宿主 loopback，
///     故 API 仅监听 127.0.0.1 时模拟器仍可经 10.0.2.2 访问。
///   - Android 真机：10.0.2.2 不存在，必须使用宿主局域网 IP，且 API 需监听 0.0.0.0。
///     IP 可在运行时经 Preferences["api_host"] 覆盖（默认值仅为开发占位）。
/// </summary>
public static class ApiConfiguration
{
    public const int ApiPort = 5773;

    /// <summary>真机连接宿主时的 Preferences 键。</summary>
    public const string PhysicalDeviceHostKey = "api_host";

    /// <summary>真机用宿主局域网 IP 的开发占位值；实际环境请用 Preferences 覆盖。</summary>
    private const string DefaultPhysicalDeviceHost = "192.168.1.100";

    public static string GetBaseUrl()
    {
        // 非 Android（Windows / MacCatalyst / iOS 模拟器/真机本地回环）
        if (DeviceInfo.Current.Platform != DevicePlatform.Android)
        {
            return $"http://localhost:{ApiPort}";
        }

        // Android 模拟器：10.0.2.2 → 宿主 loopback
        if (DeviceInfo.Current.DeviceType == DeviceType.Virtual)
        {
            return $"http://10.0.2.2:{ApiPort}";
        }

        // Android 真机：宿主局域网 IP（需 API 监听 0.0.0.0 + cleartext 白名单放行该域名）
        var host = Preferences.Get(PhysicalDeviceHostKey, DefaultPhysicalDeviceHost);
        return $"http://{host}:{ApiPort}";
    }
}
