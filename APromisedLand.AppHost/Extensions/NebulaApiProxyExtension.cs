namespace APromisedLand.AppHost.Extensions;

public static class NebulaApiProxyExtension
{
    public static IDistributedApplicationBuilder AddNebulaApiProxyService(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.NebulaApiProxy = builder.AddProject<Projects.NebulaApi_Proxy>("NebulaApi-Proxy")
            .WithHttpEndpoint(port: 9119, targetPort: 9119, name: "http", isProxied: false);
        
        if (resourceContext is { NebulaGraphFastApi: not null, NebulaGraphFastApiEndpoint: not null })
        {
            resourceContext.NebulaApiProxy.WithEnvironment("NebulaGraph-FastApi-Endpoint", resourceContext.NebulaGraphFastApiEndpoint);
            resourceContext.NebulaApiProxy.WithReference(resourceContext.NebulaGraphFastApi);
            resourceContext.NebulaApiProxy.WaitFor(resourceContext.NebulaGraphFastApi);
        }
        
        resourceContext.NebulaApiProxy.WithOtlpExporter();

        return builder;
    }
}