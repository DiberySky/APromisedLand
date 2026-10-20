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

        // ★ 容器化运行：uvicorn 作为宿主机进程无法解析 nebula 集群的 docker 内部名
        //   （nebula-graphd/nebula-storaged0，见 NebulaGraphExtension 的 --local_ip 与 ADD HOSTS），
        //   必须与集群同网络运行；NEBULA_HOSTS 用容器资源名。
        var nebulaApi = builder.AddDockerfile(
                name: "treegraph-nebulafastapi",
                contextPath: "../TreeGraph.NebulaFastApi")
            .WithHttpEndpoint(port: NebulaApiHttpPort, targetPort: 8000, name: "http")
            .WithEnvironment("NEBULA_HOSTS", "nebula-graphd:9669")
            .WithEnvironment("NEBULA_USER", "root")
            .WithEnvironment("NEBULA_PASSWORD", "nebula")
            .WithEnvironment("NEBULA_SPACE", "treegraph")
            .WithHttpHealthCheck("/api/v1/health")
            .WaitFor(context.NebulaGraph);

        context.TreeGraphNebulaFastApi = nebulaApi;
        context.TreeGraphNebulaFastApiEndpoint = nebulaApi.GetEndpoint("http");

        return builder;
    }
}
