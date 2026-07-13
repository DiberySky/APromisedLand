namespace APromisedLand.AppHost.Extensions;
using Aspire.Hosting;

public static class BlazorWasmExtension
{
    public static IDistributedApplicationBuilder AddBlazorWasm(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {

        // var blazorApp = builder.AddBlazorWasmProject<Projects.DiberyBlazorSky>("app")
        //     .WithOtlpExporter();
        
        // if (context.DiberyTreeService != null)
        // {
        //     blazorApp.WithReference(context.DiberyTreeService);
        // }
        //
        // builder.AddBlazorGateway("gateway")
        //     .WithExternalHttpEndpoints()
        //     .WithBlazorClientApp(blazorApp)
        //     .WithOtlpExporter();

        return builder;
    }
}