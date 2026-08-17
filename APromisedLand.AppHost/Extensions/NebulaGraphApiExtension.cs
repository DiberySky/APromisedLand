namespace APromisedLand.AppHost.Extensions;

public static class NebulaGraphApiExtension
{
    public static IDistributedApplicationBuilder AddNebulaGraphApiService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.NebulaGraphApiService = builder.AddProject<Projects.NebulaGraphApiService>("NebulaGraphApi-Service");
        
        if (context.NebulaConsole != null && context.NebulaGraphEndpoint != null)
        {
            context.NebulaGraphApiService.WithReference(context.NebulaGraphEndpoint);
            context.NebulaGraphApiService.WaitFor(context.NebulaConsole);
        }
        
        context.NebulaGraphApiService.WithOtlpExporter();

        return builder;
    }
}