using Aspire.Hosting.DevTunnels;

namespace APromisedLand.AppHost.Extensions;

public static class DevTunnelExtension
{
    public static IDistributedApplicationBuilder AddDevTunnel(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // if (resourceContext.YarpGateway is null || resourceContext.Keycloak is null) return builder;

        resourceContext.PublicDevTunnel = builder.AddDevTunnel("DevTunnel-public")
            .WithAnonymousAccess()
            .WithEnvironment("TUNNEL_ACCESS", "anonymous");

        if (resourceContext.Keycloak is not null)
        {
            resourceContext.PublicDevTunnel
                .WithReference(resourceContext.Keycloak.GetEndpoint("http"), new DevTunnelPortOptions
                {
                    Protocol = "https"
                });
        }

        if (resourceContext.YarpGateway is not null)
        {
            resourceContext.PublicDevTunnel.WithReference(resourceContext.YarpGateway.GetEndpoint("http"));
        }

        resourceContext.PublicDevTunnel.WithOtlpExporter();

        return builder;
    }
}