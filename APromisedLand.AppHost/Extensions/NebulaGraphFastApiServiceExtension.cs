using Aspire.Hosting;
using Aspire.Hosting.Docker;

namespace APromisedLand.AppHost.Extensions;

public static class NebulaGraphFastApiServiceExtension
{
    public static IDistributedApplicationBuilder AddNebulaGraphFastApiService(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        if (resourceContext.NebulaGraph != null)
        {
            var fastApi = builder.AddUvicornApp(
                    name: "nebula-fastapi",
                    appDirectory: "../NebulaGraphFastApiService",
                    app: "app.main:app"
                )
                .WithHttpEndpoint(port: 9339, targetPort: 9339, name: "http", isProxied: false)
                .WithEnvironment("NEBULA_ENDPOINTS", "nebula-graphd:9669")
                .WithEnvironment("API_HOST", "0.0.0.0")
                .WithEnvironment("API_PORT", "9339") // ✅ 必须加上这一行
                .WaitFor(resourceContext.NebulaGraph);

            // 如果你需要将 FastAPI 的地址传递给其他服务，可以保存到 resourceContext
            resourceContext.NebulaGraphFastApi = fastApi;
            resourceContext.NebulaGraphFastApiEndpoint = fastApi.GetEndpoint("http");
        }

        return builder;
    }
}