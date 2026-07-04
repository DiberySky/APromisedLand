namespace APromisedLand.AppHost.Extensions;

public static class MauiExtensions
{
    public static IDistributedApplicationBuilder AddMauiApp(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.Keycloak is null || context.WeatherApi is null) return builder;

        context.DiberySky = builder.AddMauiProject("DiberySky", "../DiberySky/DiberySky.csproj");

        context.DiberySky.AddWindowsDevice()
            .WithReference(context.WeatherApi)
            .WithReference(context.Keycloak);

        // 可选 Android 模拟器（注释部分）
        // context.DiberySky.AddAndroidEmulator()
        //     .WithOtlpDevTunnel()
        //     .WithReference(context.WeatherApi, context.PublicDevTunnel)
        //     .WithReference(context.Keycloak, context.PublicDevTunnel);

        return builder;
    }
}