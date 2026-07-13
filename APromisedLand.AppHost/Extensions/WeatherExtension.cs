namespace APromisedLand.AppHost.Extensions;

public static class WeatherExtension
{
    public static IDistributedApplicationBuilder AddWeatherService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // WeatherApi
        context.WeatherApi = builder.AddProject<Projects.WeatherApi>("Weather-Api");

        if (context.Keycloak is not null)
        {
            context.WeatherApi.WithReference(context.Keycloak)
                .WaitFor(context.Keycloak);
        }

        return builder;
    }
}