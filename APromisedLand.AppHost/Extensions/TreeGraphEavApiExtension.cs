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

        context.TreeGraphEavApi.WithOtlpExporter();

        return builder;
    }
}
