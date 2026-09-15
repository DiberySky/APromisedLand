namespace APromisedLand.AppHost.Extensions;
using Aspire.Hosting;

public static class BlazorWebExtension
{
    public static IDistributedApplicationBuilder AddBlazorWeb(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        var blazorApp = builder
            .AddProject<Projects.DiberyBlazorWebSky>("BlazorWeb")
            .WithExternalHttpEndpoints()
            .WithOtlpExporter();

        // ── MAFWorkFlowApi：Chat / Writer-Critic 页面 ──
        //  WithReference：注入服务发现信息，让 Blazor 能用
        //                 "https+http://MAFWorkFlowApi" 解析地址
        //  WaitFor：确保 API 就绪后再启动 Blazor
        if (resourceContext.MafWorkFlowApi is not null)
        {
            blazorApp
                .WithReference(resourceContext.MafWorkFlowApi)
                .WaitFor(resourceContext.MafWorkFlowApi);
        }

        // ── FileStorageApi：Files / Upload 页面 ──
        //  Blazor 端用 "https+http://FileStorageApi" 解析；
        //  SeaweedFS 凭证由 FileStorageApi 内部使用，Blazor 不需要感知。
        if (resourceContext.FileStorageApi is not null)
        {
            blazorApp
                .WithReference(resourceContext.FileStorageApi)
                .WaitFor(resourceContext.FileStorageApi);
        }

        resourceContext.BlazorWeb = blazorApp;

        return builder;
    }
}