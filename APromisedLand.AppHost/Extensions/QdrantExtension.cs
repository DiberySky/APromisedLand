using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// Qdrant 向量数据库编排（语义搜索）。
/// 固定宿主端口：REST 6333 / gRPC 6334。
/// ★ REST 端口挂 /healthz 探针：下游 WaitFor 等的是"可服务"而非仅"进程启动"，
///   与 vLLM 串行启动同一套同步点哲学。
/// </summary>
public static class QdrantExtension
{
    public const string GrpcEndpointName = "grpc";
    public const string RestEndpointName = "rest";
    public const int GrpcHostPort = 6334;
    public const int RestHostPort = 6333;

    public static IDistributedApplicationBuilder AddQdrant(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Qdrant = builder.AddQdrant("Qdrant")
            .WithDataVolume("qdrant-data")
            .WithEndpoint(port: GrpcHostPort, targetPort: 6334,
                scheme: "http", name: GrpcEndpointName)
            .WithEndpoint(port: RestHostPort, targetPort: 6333,
                scheme: "http", name: RestEndpointName)
            .WithLifetime(ContainerLifetime.Persistent)
            .WithHttpHealthCheck(path: "/healthz", statusCode: 200,
                endpointName: RestEndpointName);

        return builder;
    }
}
