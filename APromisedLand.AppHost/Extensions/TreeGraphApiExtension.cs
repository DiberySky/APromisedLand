using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraphApi 的 Aspire 编排扩展。
/// 复用 PostgresExtension 中声明的 TreeDb,固定主机端口 5753。
/// </summary>
public static class TreeGraphApiExtension
{
    private const int TreeGraphApiHttpPort = 5753;

    public static IDistributedApplicationBuilder AddTreeGraphApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        context.TreeGraphApi = builder
            .AddProject<Projects.TreeGraphApi>("TreeGraphApi")
            .WithHttpEndpoint(port: TreeGraphApiHttpPort, name: "http");

        // 注入 TreeGraphDb 引用(Aspire 会自动写入 ConnectionStrings:TreeGraphDb)
        if (context.TreeGraphDb is not null)
        {
            context.TreeGraphApi
                .WithReference(context.TreeGraphDb)
                .WaitFor(context.TreeGraphDb);
        }
        else
        {
            Console.WriteLine("[TreeGraphApi] ⚠️ context.TreeGraphDb 为 null,未注入数据库引用");
        }

        // 健康检查:Swagger 端点 + /api/tree/roots 探活
        context.TreeGraphApi
            .WithHttpHealthCheck(
                path: "/api/tree/roots",
                statusCode: 200,
                endpointName: "http");

        context.TreeGraphApi.WithOtlpExporter();

        return builder;
    }
}
