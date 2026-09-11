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
        AppHostContext context)
    {
        // ★ 固定宿主机端口 5100
        context.MafRagService = builder
            .AddProject<Projects.MAFRagService>("MafRagService")
            .WithHttpEndpoint(port: MafRagHttpPort, name: "http");

        // ========== 内部资源：走标准 WithReference ==========
        if (context.MetadataDb is not null)
            context.MafRagService.WithReference(context.MetadataDb);

        if (context.HangfireDb is not null)
            context.MafRagService.WithReference(context.HangfireDb);

        if (context.Redis is not null)
            context.MafRagService.WithReference(context.Redis);

        // Ollama 服务端：用于 /api/tags 健康探测
        if (context.Ollama is not null)
        {
            context.MafRagService
                .WithReference(context.Ollama)
                .WaitFor(context.Ollama);
        }

        // 聊天模型：注入 ConnectionStrings__chat-model
        if (context.ChatModel is not null)
        {
            context.MafRagService
                .WithReference(context.ChatModel)
                .WaitFor(context.ChatModel);
        }

        // 嵌入模型：注入 ConnectionStrings__embedding
        if (context.Embedding is not null)
        {
            context.MafRagService
                .WithReference(context.Embedding)
                .WaitFor(context.Embedding);
        }

        // ========== 外部资源：走显式连接字符串 ==========

        WireExternalService(
            context.MafRagService,
            context.NebulaGraph,
            endpointName: "graph",
            connectionStringName: "nebula",
            scheme: "thrift");

        WireExternalService(
            context.MafRagService,
            context.Weaviate,
            endpointName: "http",
            connectionStringName: "weaviate",
            scheme: "http");

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