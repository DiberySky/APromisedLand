namespace APromisedLand.AppHost.Extensions;
using Aspire.Hosting;

public static class BlazorWasmExtension
{
    public static IDistributedApplicationBuilder AddBlazorWasm(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {

        // var blazorApp = builder.AddBlazorWasmProject<Projects.DiberyBlazorSky>("app")
        //     .WithOtlpExporter();
        
        // if (resourceContext.DiberyTreeService != null)
        // {
        //     blazorApp.WithReference(resourceContext.DiberyTreeService);
        // }
        //
        // builder.AddBlazorGateway("gateway")
        //     .WithExternalHttpEndpoints()
        //     .WithBlazorClientApp(blazorApp)
        //     .WithOtlpExporter();

        return builder;
    }
}