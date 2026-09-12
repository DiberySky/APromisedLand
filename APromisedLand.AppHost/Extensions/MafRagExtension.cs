using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost.Extensions;

public static class MafRagExtension
{
    /// <summary>
    /// MafRagService 固定的宿主机 HTTP 端口。
    /// 便于本地开发 / curl / Postman 直接访问，无需每次从日志提取。
    /// </summary>
    private const int MafRagHttpPort = 5100;

    public static IDistributedApplicationBuilder AddMafRagService(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // ★ 固定宿主机端口 5100
        resourceContext.MafRagService = builder
            .AddProject<Projects.MAFRagService>("MafRagService")
            .WithHttpEndpoint(port: MafRagHttpPort, name: "http");

        // ========== 内部资源：走标准 WithReference ==========
        if (resourceContext.MetadataDb is not null)
            resourceContext.MafRagService.WithReference(resourceContext.MetadataDb);

        if (resourceContext.HangfireDb is not null)
            resourceContext.MafRagService.WithReference(resourceContext.HangfireDb);

        if (resourceContext.Redis is not null)
            resourceContext.MafRagService.WithReference(resourceContext.Redis);

        // Ollama 服务端：用于 /api/tags 健康探测
        if (resourceContext.Ollama is not null)
        {
            resourceContext.MafRagService
                .WithReference(resourceContext.Ollama)
                .WaitFor(resourceContext.Ollama);
        }

        // 聊天模型：注入 ConnectionStrings__chat-model
        if (resourceContext.ChatModel is not null)
        {
            resourceContext.MafRagService
                .WithReference(resourceContext.ChatModel)
                .WaitFor(resourceContext.ChatModel);
        }

        // 嵌入模型：注入 ConnectionStrings__embedding
        if (resourceContext.Embedding is not null)
        {
            resourceContext.MafRagService
                .WithReference(resourceContext.Embedding)
                .WaitFor(resourceContext.Embedding);
        }

        // ========== 外部资源：走显式连接字符串 ==========

        WireExternalService(
            resourceContext.MafRagService,
            resourceContext.NebulaGraph,
            endpointName: "graph",
            connectionStringName: "nebula",
            scheme: "thrift");

        WireExternalService(
            resourceContext.MafRagService,
            resourceContext.Weaviate,
            endpointName: "http",
            connectionStringName: "weaviate",
            scheme: "http");

        WireExternalService(
            resourceContext.MafRagService,
            resourceContext.SeaweedS3,
            endpointName: "s3",
            connectionStringName: "seaweedfs",
            scheme: "http");

        resourceContext.MafRagService.WithOtlpExporter();

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