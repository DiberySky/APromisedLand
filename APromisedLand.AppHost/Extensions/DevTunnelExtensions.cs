using Aspire.Hosting.DevTunnels;

namespace APromisedLand.AppHost.Extensions;

public static class DevTunnelExtensions
{
    public static IDistributedApplicationBuilder AddDevTunnel(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.YarpGateway is null || context.Keycloak is null) return builder;
        
        context.PublicDevTunnel = builder.AddDevTunnel("DevTunnel-public")
            .WithAnonymousAccess()
            .WithEnvironment("TUNNEL_ACCESS", "anonymous")
            .WithReference(context.Keycloak.GetEndpoint("http"), new DevTunnelPortOptions
            {
                Protocol = "https"
            })
            .WithReference(context.YarpGateway.GetEndpoint("http"));

        return builder;
    }
}