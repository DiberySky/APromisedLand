using Aspire.Hosting;
using Aspire.Hosting.Docker;

namespace APromisedLand.AppHost.Extensions;

public static class NebulaGraphFastApiExtension
{
    public static IDistributedApplicationBuilder AddNebulaGraphFastApi(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.NebulaGraph != null)
        {
            var fastApi = builder.AddUvicornApp(
                    name: "nebula-fastapi", // 服务在 Aspire 中的名称
                    appDirectory: "../NebulaGraphFastApiService", // 指向你的 Python 项目目录
                    app: "app.main:app" // 指向 ASGI 应用，格式为 "模块名:变量名"
                )
                .WithHttpEndpoint(port: 8668, targetPort: 8668, name: "http") // 暴露 HTTP 端口
                .WithEnvironment("NEBULA_ENDPOINTS", "nebula-graphd:9669") // 传递 NebulaGraph 连接信息
                .WithEnvironment("API_HOST", "0.0.0.0") // 传递其他环境变量
                .WaitFor(context.NebulaGraph); // 等待 NebulaGraph 就绪

            // 如果你需要将 FastAPI 的地址传递给其他服务，可以保存到 context
            context.NebulaGraphFastApi = fastApi;
            context.NebulaGraphFastApiEndpoint = fastApi.GetEndpoint("http");
        }

        return builder;
    }
}