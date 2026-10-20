using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraph.Api(EAV 动态类型系统)的 Aspire 编排扩展。
/// 复用 PostgresExtension 中声明的 TreeGraphDb,固定主机端口 5773。
/// </summary>
public static class TreeGraphEavApiExtension
{
    private const int TreeGraphEavApiHttpPort = 5773;

    public static IDistributedApplicationBuilder AddTreeGraphEavApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        // ★ 修复 P1-1：资源名统一小写，与 Blazor 端 https+http://treegrapheavapi 一致
        context.TreeGraphEavApi = builder
            .AddProject<Projects.TreeGraph_Api>("treegrapheavapi")
            .WithHttpEndpoint(port: TreeGraphEavApiHttpPort, name: "http");

        if (context.TreeGraphDb is null)
        {
            // ★ 修复 P2-1：不再静默继续，尽早暴露配置错误
            throw new InvalidOperationException(
                "AddTreeGraphEavApi 前需先调用 builder.AddPostgres(context)");
        }

        context.TreeGraphEavApi
            .WithReference(context.TreeGraphDb)
            .WaitFor(context.TreeGraphDb);

        // 健康检查：实体类型目录端点（仅读元数据，空表也返回 200 + []）
        context.TreeGraphEavApi
            .WithHttpHealthCheck(
                path: "/api/eav/entity-types",
                statusCode: 200,
                endpointName: "http");

        // ══════════════════════════════════════════════════════
        // ★ 语义搜索依赖接线
        // ══════════════════════════════════════════════════════

        // RabbitMQ（宿主变更事件管道；注入 ConnectionStrings__RabbitMQ）
        if (context.RabbitMq is not null)
        {
            context.TreeGraphEavApi
                .WithReference(context.RabbitMq)
                .WaitFor(context.RabbitMq);
        }
        else
        {
            Console.WriteLine("[TreeGraphEavApi] ⚠️ context.RabbitMq 为 null，向量同步消费者将连接 localhost:5672");
        }

        // Qdrant（注入 ConnectionStrings__Qdrant）
        if (context.Qdrant is not null)
        {
            context.TreeGraphEavApi
                .WithReference(context.Qdrant)
                .WaitFor(context.Qdrant);
        }
        else
        {
            Console.WriteLine("[TreeGraphEavApi] ⚠️ context.Qdrant 为 null，语义搜索将连接 localhost:6334");
        }

        // vLLM Embedding（bge-m3 向量化；ContainerResource 无连接串，手动注入端点）
        if (context.VllmEmbed is not null)
        {
            var embedEndpoint = context.VllmEmbed.GetEndpoint(VllmExtension.EmbeddingHttpEndpointName);

            context.TreeGraphEavApi
                .WaitFor(context.VllmEmbed)
                .WithEnvironment("Embedding__Endpoint",
                    ReferenceExpression.Create($"{embedEndpoint}/v1/"));

            Console.WriteLine($"[TreeGraphEavApi] Embedding 端点 → {embedEndpoint}/v1/");
        }
        else
        {
            Console.WriteLine("[TreeGraphEavApi] ⚠️ context.VllmEmbed 为 null，向量化将连接 localhost:5719");
        }

        // TreeGraph.NebulaFastApi（图同步 REST 网关；注入 NebulaApi:Endpoint）
        if (context.TreeGraphNebulaFastApiEndpoint is not null)
        {
            context.TreeGraphEavApi
                .WithEnvironment("NebulaApi__Endpoint",
                    ReferenceExpression.Create($"{context.TreeGraphNebulaFastApiEndpoint}/"));

            if (context.TreeGraphNebulaFastApi is not null)
                context.TreeGraphEavApi.WaitFor(context.TreeGraphNebulaFastApi);
        }
        else
        {
            Console.WriteLine("[TreeGraphEavApi] ⚠️ context.TreeGraphNebulaFastApiEndpoint 为 null，图同步将连接 localhost:9393");
        }

        context.TreeGraphEavApi.WithOtlpExporter();

        return builder;
    }
}
