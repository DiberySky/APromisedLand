namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraph MAUI 客户端的 Aspire 编排扩展。
///
/// 设计要点：
///   1. 按宿主平台选择默认设备：
///      - Windows 宿主 → AddWindowsDevice()（对应 csproj 中的 net10.0-windows10.0.19041.0 TFM）
///      - 非 Windows 宿主（macOS/Linux）→ AddAndroidEmulator()
///        （csproj 中 Windows TFM 仅在 Windows 主机上包含，macOS 上调用 AddWindowsDevice 会失败）
///
///   2. 注入服务发现引用：
///      WithReference(treegrapheavapi) 会让 Aspire 向 MAUI 进程注入
///        services__treegrapheavapi__http__0=http://127.0.0.1:<port>
///      客户端 AddServiceDefaults() 中的 AddServiceDiscovery() 识别该变量，
///      使 MauiProgram 里的 client.BaseAddress = http://treegrapheavapi 能正确解析。
///
///   3. OTLP 导出：WithOtlpExporter() 注入 OTEL_EXPORTER_OTLP_ENDPOINT，
///      由 APromisedLand.MauiServiceDefaults/Extensions.cs 中的 AddOpenTelemetryExporters 消费。
/// </summary>
public static class TreeGraphMauiExtension
{
    public static IDistributedApplicationBuilder AddTreeGraphMaui(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.TreeGraphMauiSky = builder.AddMauiProject(
            "TreeGraphMauiSky",
            "../TreeGraph.Blazor.Maui/TreeGraph.Blazor.Maui.csproj");

        if (OperatingSystem.IsWindows())
        {
            // ── Windows 宿主：桌面 MAUI 目标 ──
            var winDevice = resourceContext.TreeGraphMauiSky.AddWindowsDevice();

            // 身份认证（恢复时启用）
            // if (resourceContext.Keycloak is not null
            //     && resourceContext.PublicDevTunnel is not null)
            // {
            //     winDevice.WithReference(
            //         resourceContext.Keycloak, resourceContext.PublicDevTunnel);
            // }

            // ★ EAV API 服务发现引用
            if (resourceContext.TreeGraphEavApi is not null)
                winDevice.WithReference(resourceContext.TreeGraphEavApi);

            // ★ OTLP 导出（端点由 Aspire 注入，客户端 ServiceDefaults 消费）
            winDevice.WithOtlpExporter();
        }
        else
        {
            // ── macOS / Linux 宿主：Android 模拟器 ──
            // Windows TFM 未包含在 csproj（Condition="IsOSPlatform('windows')"），
            // 因此不能在此分支调用 AddWindowsDevice()。
            var androidDevice = resourceContext.TreeGraphMauiSky.AddAndroidEmulator();

            // ★ EAV API 服务发现引用
            //   Android 模拟器与宿主位于不同网络命名空间，Aspire 会通过 adb reverse
            //   将宿主端口反向映射到模拟器内，因此 WithReference 的服务发现变量
            //   在模拟器内仍然有效。
            if (resourceContext.TreeGraphEavApi is not null)
                androidDevice.WithReference(resourceContext.TreeGraphEavApi);

            // ★ OTLP 导出
            //   默认 WithOtlpExporter() 会注入宿主的 OTLP endpoint（http://127.0.0.1:...），
            //   Android 模拟器同样经 adb reverse 可达；若模拟器跨设备不可达，
            //   可改用 WithOtlpDevTunnel()（需 PublicDevTunnel 资源）。
            androidDevice.WithOtlpExporter();
            // androidDevice.WithOtlpDevTunnel();

            // 身份认证（恢复时启用）
            // if (resourceContext.Keycloak is not null
            //     && resourceContext.PublicDevTunnel is not null)
            // {
            //     androidDevice.WithReference(
            //         resourceContext.Keycloak, resourceContext.PublicDevTunnel);
            // }
        }

        return builder;
    }
}