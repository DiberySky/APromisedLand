namespace APromisedLand.AppHost.Extensions;

public static class MauiExtension
{
    public static IDistributedApplicationBuilder AddMauiApp(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.DiberySky = builder.AddMauiProject("DiberySky", "../DiberySky/DiberySky.csproj");

        var winDevice = resourceContext.DiberySky.AddWindowsDevice();

        if (resourceContext.Keycloak is not null && resourceContext.PublicDevTunnel is not null)
        {
            winDevice.WithReference(resourceContext.Keycloak, resourceContext.PublicDevTunnel);
        }

        if (resourceContext.DiberyTreeService is not null)
        {
            winDevice.WithReference(resourceContext.DiberyTreeService);
        }

        winDevice.WithOtlpExporter();

        // 可选 Android 模拟器（注释部分）
        // resourceContext.DiberySky.AddAndroidEmulator()
        //     .WithOtlpDevTunnel()
        //     .WithReference(resourceContext.WeatherApi, resourceContext.PublicDevTunnel)
        //     .WithReference(resourceContext.Keycloak, resourceContext.PublicDevTunnel);

        return builder;
    }
}