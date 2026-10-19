using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraph.NebulaApi（Python FastAPI → NebulaGraph REST 网关）的 Aspire 编排扩展。
/// 依赖 AddNebulaGraph 声明的 graphd 容器；固定主机端口 9393。
/// </summary>
public static class TreeGraphNebulaApiExtension
{
    private const int NebulaApiHttpPort = 9393;

    public static IDistributedApplicationBuilder AddTreeGraphNebulaApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        if (context.NebulaGraph is null)
        {
            throw new InvalidOperationException(
                "AddTreeGraphNebulaApi 前需先调用 builder.AddNebulaGraph(context)");
        }

        var nebulaApi = builder.AddUvicornApp(
                name: "treegraph-nebulaapi",
                appDirectory: "../TreeGraph.NebulaApi",
                app: "app.main:app")
            .WithHttpEndpoint(port: NebulaApiHttpPort, targetPort: NebulaApiHttpPort,
                name: "http", isProxied: false)
            .WithEnvironment("NEBULA_HOSTS", "nebula-graphd:9669")
            .WithEnvironment("NEBULA_SPACE", "treegraph")
            .WithEnvironment("HOST", "0.0.0.0")
            .WithEnvironment("PORT", NebulaApiHttpPort.ToString())
            .WaitFor(context.NebulaGraph);

        context.TreeGraphNebulaApi = nebulaApi;
        context.TreeGraphNebulaApiEndpoint = nebulaApi.GetEndpoint("http");

        return builder;
    }
}
