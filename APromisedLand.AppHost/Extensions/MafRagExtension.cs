using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost.Extensions;

public static class MafRagExtension
{
    public static IDistributedApplicationBuilder AddMafRagService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.MafRagService = builder.AddProject<Projects.MAFRagService>("MafRagService");

        // ========== 内部资源：走标准 WithReference ==========
        // WithReference 已隐式包含 WaitFor 语义，无需额外 WaitFor
        if (context.MetadataDb is not null)
            context.MafRagService.WithReference(context.MetadataDb);

        if (context.HangfireDb is not null)
            context.MafRagService.WithReference(context.HangfireDb);

        if (context.Redis is not null)
            context.MafRagService.WithReference(context.Redis);

        // ========== 外部资源：走显式连接字符串 ==========
        // NebulaGraph：Thrift RPC，9669 端口
        WireExternalService(
            context.MafRagService,
            context.NebulaGraph,
            endpointName: "graph",
            connectionStringName: "nebula",
            scheme: "thrift");

        // Weaviate：HTTP REST，8080 端口
        WireExternalService(
            context.MafRagService,
            context.Weaviate,
            endpointName: "http",
            connectionStringName: "weaviate",
            scheme: "http");

        // SeaweedFS：业务侧连 filer 的 POSIX API（8888），不是 master，也不是 S3
        WireExternalService(
            context.MafRagService,
            context.SeaweedFiler,
            endpointName: "http",
            connectionStringName: "seaweedfs",
            scheme: "http");

        context.MafRagService.WithOtlpExporter();

        return builder;
    }

    /// <summary>
    /// 为一个外部资源生成连接字符串环境变量，并让服务等待它就绪。
    /// <para>连接字符串格式：<c>{scheme}://{host}:{port}</c></para>
    /// <para>环境变量键名：<c>ConnectionStrings__{connectionStringName}</c>（双下划线，对应 IConfiguration 的层级分隔符）</para>
    /// <para>服务端读取：<c>configuration.GetConnectionString("{connectionStringName}")</c></para>
    /// </summary>
    /// <param name="service">要注入环境变量的服务资源</param>
    /// <param name="resource">提供端点的外部资源；为 null 时跳过</param>
    /// <param name="endpointName">外部资源上声明的端点名（如 "graph"、"http"）</param>
    /// <param name="connectionStringName">连接字符串逻辑名（如 "nebula"、"weaviate"、"seaweedfs"）</param>
    /// <param name="scheme">URL scheme（如 "thrift"、"http"）</param>
    private static void WireExternalService(
        IResourceBuilder<ProjectResource> service,
        IResourceBuilder<IResourceWithEndpoints>? resource,
        string endpointName,
        string connectionStringName,
        string scheme)
    {
        if (resource is null) return;

        var ep = resource.GetEndpoint(endpointName);
        service.WithEnvironment(
            $"ConnectionStrings__{connectionStringName}",
            $"{scheme}://{ep.Property(EndpointProperty.HostAndPort)}");
        service.WaitFor(resource);
    }
}