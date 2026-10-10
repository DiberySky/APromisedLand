namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraph.Maui 客户端的 Aspire 编排扩展（版式克隆 TreeGraphBlazorMauiExtension）。
///
/// 设计要点：
///   1. 按宿主平台选择默认设备：
///      - Windows 宿主 → AddWindowsDevice()（对应 csproj 中的 net10.0-windows10.0.19041.0 TFM）
///      - 非 Windows 宿主（macOS/Linux）→ AddAndroidEmulator()
///
///   2. 注入服务发现引用：WithReference(treegrapheavapi) 注入
///        services__treegrapheavapi__http__0=http://127.0.0.1:<port>
///      注意：TreeGraph.Maui 目前是 .NET 10 MAUI+Blazor 混合模板起步项目，
///      尚未引入 APromisedLand.MauiServiceDefaults（AddServiceDefaults /
///      AddServiceDiscovery / OpenTelemetry），因此注入的环境变量暂未被消费；
///      接入服务端调用时需补 AddServiceDefaults()。
///
///   3. OTLP 导出：WithOtlpExporter() 注入 OTEL_EXPORTER_OTLP_ENDPOINT，
///      同样待应用侧引入 OpenTelemetry 后生效。
/// </summary>
public static class TreeGraphMauiExtension
{
    public static IDistributedApplicationBuilder AddTreeGraphMaui(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.TreeGraphMauiSky = builder.AddMauiProject(
            "TreeGraphMaui",
            "../TreeGraph.Maui/TreeGraph.Maui.csproj");

        if (OperatingSystem.IsWindows())
        {
            // ── Windows 宿主：桌面 MAUI 目标 ──
            var winDevice = resourceContext.TreeGraphMauiSky.AddWindowsDevice();
            WireDevice(winDevice, resourceContext);
        }
        else
        {
            // ── macOS / Linux 宿主：Android 模拟器 ──
            var androidDevice = resourceContext.TreeGraphMauiSky.AddAndroidEmulator();
            WireDevice(androidDevice, resourceContext);
        }

        return builder;

        // ★ 统一注入 EAV API 引用 + OTLP，避免 Windows / Android 分支重复。
        static void WireDevice<T>(
            IResourceBuilder<T> device,
            AppHostResourceContext ctx) where T : IResourceWithEnvironment
        {
            // ★ EAV API 服务发现引用（Android 模拟器经 adb reverse 可达宿主端口）
            if (ctx.TreeGraphEavApi is not null)
                device.WithReference(ctx.TreeGraphEavApi);

            // ★ OTLP 导出（跨设备不可达时可改用 WithOtlpDevTunnel()）
            device.WithOtlpExporter();
        }
    }
}
