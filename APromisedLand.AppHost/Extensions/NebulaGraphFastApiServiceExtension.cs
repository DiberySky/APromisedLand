using Aspire.Hosting;
using Aspire.Hosting.Docker;

namespace APromisedLand.AppHost.Extensions;

public static class NebulaGraphFastApiExtension
{
    public static IDistributedApplicationBuilder AddNebulaGraphFastApiService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.NebulaGraph != null)
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
                .WaitFor(context.NebulaGraph);

            // 如果你需要将 FastAPI 的地址传递给其他服务，可以保存到 context
            context.NebulaGraphFastApi = fastApi;
            context.NebulaGraphFastApiEndpoint = fastApi.GetEndpoint("http");
        }

        return builder;
    }
}