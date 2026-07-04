namespace APromisedLand.AppHost.Extensions;

public static class IdentityExtensions
{
    public static IDistributedApplicationBuilder AddIdentity(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.Keycloak = builder.AddKeycloak("Keycloak", 8323)
            .WithDataVolume("keycloak-data")
            .WithOtlpExporter();

        return builder;
    }
}