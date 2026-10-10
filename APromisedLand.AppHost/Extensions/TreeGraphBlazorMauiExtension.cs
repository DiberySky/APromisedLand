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
public static class TreeGraphBlazorMauiExtension
{
    public static IDistributedApplicationBuilder AddTreeGraphBlazorMaui(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.TreeGraphBlazorMauiSky = builder.AddMauiProject(
            "TreeGraphBlazorMaui",
            "../TreeGraph.Blazor.Maui/TreeGraph.Blazor.Maui.csproj");

        if (OperatingSystem.IsWindows())
        {
            // ── Windows 宿主：桌面 MAUI 目标 ──
            var winDevice = resourceContext.TreeGraphBlazorMauiSky.AddWindowsDevice();
            WireDevice(winDevice, resourceContext);
        }
        else
        {
            // ── macOS / Linux 宿主：Android 模拟器 ──
            // Windows TFM 未包含在 csproj（Condition="IsOSPlatform('windows')"），
            // 因此不能在此分支调用 AddWindowsDevice()。
            var androidDevice = resourceContext.TreeGraphBlazorMauiSky.AddAndroidEmulator();
            WireDevice(androidDevice, resourceContext);
        }

        return builder;

        // ★ 统一注入 EAV API 引用 + OTLP，避免 Windows / Android 分支重复；
        //   新增 iOS / MacCatalyst 分支时只需一行 WireDevice(...)。
        static void WireDevice<T>(
            IResourceBuilder<T> device,
            AppHostResourceContext ctx) where T : IResourceWithEnvironment
        {
            // ★ EAV API 服务发现引用
            //   WithReference(treegrapheavapi) 注入 services__treegrapheavapi__http__0；
            //   Android 模拟器与宿主位于不同网络命名空间，Aspire 会通过 adb reverse
            //   将宿主端口反向映射到模拟器内，因此服务发现变量在模拟器内仍然有效。
            if (ctx.TreeGraphEavApi is not null)
                device.WithReference(ctx.TreeGraphEavApi);

            // ★ OTLP 导出
            //   默认 WithOtlpExporter() 会注入宿主的 OTLP endpoint（http://127.0.0.1:...），
            //   Android 模拟器同样经 adb reverse 可达；若模拟器跨设备不可达，
            //   可改用 WithOtlpDevTunnel()（需 PublicDevTunnel 资源）。
            device.WithOtlpExporter();
            // device.WithOtlpDevTunnel();

            // 身份认证（恢复时启用）
            // if (ctx.Keycloak is not null && ctx.PublicDevTunnel is not null)
            //     device.WithReference(ctx.Keycloak, ctx.PublicDevTunnel);
        }
    }
}