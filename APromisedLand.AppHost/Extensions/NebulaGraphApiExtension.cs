namespace APromisedLand.AppHost.Extensions;

public static class NebulaGraphApiExtension
{
    public static IDistributedApplicationBuilder AddNebulaGraphApiService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.NebulaGraphFastApiService = builder.AddProject<Projects.NebulaGraphApiService>("NebulaGraphApi-Service");
        
        if (context.NebulaConsole != null && context.NebulaGraphEndpoint != null)
        {
            context.NebulaGraphFastApiService.WithReference(context.NebulaGraphEndpoint);
            context.NebulaGraphFastApiService.WaitFor(context.NebulaConsole);
        }
        
        context.NebulaGraphFastApiService.WithOtlpExporter();

        return builder;
    }
}