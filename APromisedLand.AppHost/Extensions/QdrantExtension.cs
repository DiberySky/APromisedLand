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
            // isProxied: false → 由 docker 直接发布宿主端口，绕过 DCP 代理
            //（代理模式下固定端口偶发不绑定，健康检查探针打不通导致 WaitFor 挂起）。
            .WithEndpoint(port: GrpcHostPort, targetPort: 6334,
                scheme: "http", name: GrpcEndpointName, isProxied: false)
            .WithEndpoint(port: RestHostPort, targetPort: 6333,
                scheme: "http", name: RestEndpointName, isProxied: false)
            .WithLifetime(ContainerLifetime.Persistent)
            // 离线环境访问不了 telemetry.qdrant.io，关闭遥测消除 ERROR 噪音
            .WithEnvironment("QDRANT__TELEMETRY_DISABLED", "true")
            .WithHttpHealthCheck(path: "/healthz", statusCode: 200,
                endpointName: RestEndpointName);

        return builder;
    }
}
