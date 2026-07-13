namespace APromisedLand.AppHost.Extensions;

public static class DiberyMauiExtension
{
    public static IDistributedApplicationBuilder AddDiberyMauiSky(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.DiberyMauiSky = builder.AddMauiProject("DiberyMauiSky", "../DiberyMauiSky/DiberyMauiSky.csproj");

        var winDevice = context.DiberyMauiSky.AddWindowsDevice();

        if (context.Keycloak is not null && context.PublicDevTunnel is not null)
        {
            winDevice.WithReference(context.Keycloak, context.PublicDevTunnel);
        }

        if (context.DiberyTreeService is not null)
        {
            winDevice.WithReference(context.DiberyTreeService);
        }

        winDevice.WithOtlpExporter();

        // 可选 Android 模拟器（注释部分）
        // context.DiberySky.AddAndroidEmulator()
        //     .WithOtlpDevTunnel()
        //     .WithReference(context.WeatherApi, context.PublicDevTunnel)
        //     .WithReference(context.Keycloak, context.PublicDevTunnel);

        return builder;
    }
}