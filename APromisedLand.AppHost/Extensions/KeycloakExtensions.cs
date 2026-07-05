namespace APromisedLand.AppHost.Extensions;

public static class KeycloakExtensions
{
    public static IDistributedApplicationBuilder AddKeycloak(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.Keycloak = builder.AddKeycloak("Keycloak", 8323)
            .WithDataVolume("keycloak-data")
            .WithOtlpExporter();

        // builder.AddKeycloak("Keycloak8323", 8111)
        //     .WithDataVolume("keycloak323-data")
        //     .WithOtlpExporter();

        return builder;
    }
}