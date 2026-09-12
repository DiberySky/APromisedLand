namespace APromisedLand.AppHost.Extensions;

public static class WeatherExtension
{
    public static IDistributedApplicationBuilder AddWeatherService(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // WeatherApi
        resourceContext.WeatherApi = builder.AddProject<Projects.WeatherApi>("Weather-Api");

        if (resourceContext.Keycloak is not null)
        {
            resourceContext.WeatherApi.WithReference(resourceContext.Keycloak)
                .WaitFor(resourceContext.Keycloak);
        }

        return builder;
    }
}