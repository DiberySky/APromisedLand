using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraph.NebulaFastApi（Python FastAPI → NebulaGraph REST 网关）的 Aspire 编排扩展。
/// 依赖 AddNebulaGraph 声明的 graphd 容器；固定主机端口 9393。
/// </summary>
public static class TreeGraphNebulaFastApiExtension
{
    private const int NebulaApiHttpPort = 9393;

    public static IDistributedApplicationBuilder AddTreeGraphNebulaFastApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        if (context.NebulaGraph is null)
        {
            throw new InvalidOperationException(
                "AddTreeGraphNebulaFastApi 前需先调用 builder.AddNebulaGraph(context)");
        }

        var nebulaApi = builder.AddUvicornApp(
                name: "treegraph-nebulafastapi",
                appDirectory: "../TreeGraph.NebulaFastApi",
                app: "app.main:app")
            .WithHttpEndpoint(port: NebulaApiHttpPort, targetPort: NebulaApiHttpPort,
                name: "http", isProxied: false)
            .WithEnvironment("NEBULA_HOSTS", "nebula-graphd:9669")
            .WithEnvironment("NEBULA_SPACE", "treegraph")
            .WithEnvironment("HOST", "0.0.0.0")
            .WithEnvironment("PORT", NebulaApiHttpPort.ToString())
            .WaitFor(context.NebulaGraph);

        context.TreeGraphNebulaFastApi = nebulaApi;
        context.TreeGraphNebulaFastApiEndpoint = nebulaApi.GetEndpoint("http");

        return builder;
    }
}
