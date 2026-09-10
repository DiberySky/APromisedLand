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

        // ============================================================
        // 【修复 #2】SeaweedFS：服务端使用 AmazonS3Client，必须连 S3 网关（8333）。
        // 前提：AppHostContext 中新增 SeaweedS3 属性，并在 AppHost 中创建对应资源。
        // ============================================================
        WireExternalService(
            context.MafRagService,
            context.SeaweedS3,
            endpointName: "s3",
            connectionStringName: "seaweedfs",
            scheme: "http");

        context.MafRagService.WithOtlpExporter();

        return builder;
    }

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