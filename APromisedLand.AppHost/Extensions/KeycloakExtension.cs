namespace APromisedLand.AppHost.Extensions;

public static class KeycloakExtension
{
    public static IDistributedApplicationBuilder AddKeycloak(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Keycloak = builder.AddKeycloak("Keycloak", 8323)
            .WithDataVolume("keycloak-data")
            .WithOtlpExporter();

        return builder;
    }
}