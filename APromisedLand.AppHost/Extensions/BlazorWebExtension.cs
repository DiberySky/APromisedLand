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

        // 建立与 API 的引用：
        //  - WithReference：注入 ConnectionStrings / 服务发现信息，
        //    让 Blazor Web App 能用 "https+http://MafStatefulApi" 解析地址
        //  - WaitFor：确保 API 就绪后再启动 Blazor
        if (resourceContext.MafWorkFlowApi is not null)
        {
            blazorApp
                .WithReference(resourceContext.MafWorkFlowApi)
                .WaitFor(resourceContext.MafWorkFlowApi);
        }

        return builder;
    }
}