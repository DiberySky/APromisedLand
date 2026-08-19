namespace APromisedLand.AppHost.Extensions;

public static class NebulaApiProxyExtension
{
    public static IDistributedApplicationBuilder AddNebulaApiProxyService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.NebulaApiProxy = builder.AddProject<Projects.NebulaApi_Proxy>("NebulaApi-Proxy")
            .WithHttpEndpoint(port: 9119, targetPort: 9119, name: "http", isProxied: false);
        
        if (context is { NebulaGraphFastApi: not null, NebulaGraphFastApiEndpoint: not null })
        {
            context.NebulaApiProxy.WithEnvironment("NebulaGraph-FastApi-Endpoint", context.NebulaGraphFastApiEndpoint);
            context.NebulaApiProxy.WithReference(context.NebulaGraphFastApi);
            context.NebulaApiProxy.WaitFor(context.NebulaGraphFastApi);
        }
        
        context.NebulaApiProxy.WithOtlpExporter();

        return builder;
    }
}