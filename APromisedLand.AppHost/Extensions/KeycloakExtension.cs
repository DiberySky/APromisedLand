namespace APromisedLand.AppHost.Extensions;

public static class KeycloakExtension
{
    public static IDistributedApplicationBuilder AddKeycloak(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.Keycloak = builder.AddKeycloak("Keycloak", 8323)
            .WithDataVolume("keycloak-data")
            .WithOtlpExporter();

        return builder;
    }
}