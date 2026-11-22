using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// MafRagApiService 的 Aspire 编排扩展。
/// 复用 PostgresExtension 中声明的 TreeDb,固定主机端口 5753。
/// </summary>
public static class MafRagApiServiceExtension
{
    private const int MafRagApiServiceHttpPort = 5753;

    public static IDistributedApplicationBuilder AddMafRagApiService(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        context.MafRagApiService = builder
            .AddProject<Projects.MafRagApiService>("MafRagApiService")
            .WithHttpEndpoint(port: MafRagApiServiceHttpPort, name: "http");

        // 注入 TreeGraphDb 引用(Aspire 会自动写入 ConnectionStrings:TreeGraphDb)
        if (context.TreeGraphDb is not null)
        {
            context.MafRagApiService
                .WithReference(context.TreeGraphDb)
                .WaitFor(context.TreeGraphDb);
        }
        else
        {
            Console.WriteLine("[MafRagApiService] ⚠️ context.TreeGraphDb 为 null,未注入数据库引用");
        }

        // 健康检查:Swagger 端点 + /api/tree/roots 探活
        context.MafRagApiService
            .WithHttpHealthCheck(
                path: "/api/tree/roots",
                statusCode: 200,
                endpointName: "http");

        context.MafRagApiService.WithOtlpExporter();

        return builder;
    }
}
