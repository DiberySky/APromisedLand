using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// MafRagTreeGraph 前端 Blazor Server 应用的 Aspire 编排扩展。
/// 固定主机端口 5763,通过环境变量注入 TreeGraphApi 的 http 端点。
/// </summary>
public static class MafRagTreeGraphExtension
{
    private const int MafRagTreeGraphHttpPort = 5763;

    public static IDistributedApplicationBuilder AddMafRagTreeGraph(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        context.MafRagTreeGraph = builder
            .AddProject<Projects.MafRagTreeGraph>("MafRagTreeGraph")
            .WithHttpEndpoint(port: MafRagTreeGraphHttpPort, name: "http");

        // 注入 TreeGraphApi 端点(由 TreeGraphApiExtension 声明,端口 5753)
        // MafRagTreeGraph 的 Program.cs 通过 AddHttpClient<TreeApiClient> 读取
        // TreeGraphApi__BaseUrl 或硬编码 localhost:5753,这里用 WithReference 注入
        if (context.TreeGraphApi is not null)
        {
            var treeGraphApiEndpoint = context.TreeGraphApi.GetEndpoint("http");

            context.MafRagTreeGraph
                .WaitFor(context.TreeGraphApi)
                .WithEnvironment("TreeGraphApi__BaseUrl", treeGraphApiEndpoint);

            Console.WriteLine($"[MafRagTreeGraph] TreeGraphApi 端点 → {treeGraphApiEndpoint}");
        }
        else
        {
            Console.WriteLine("[MafRagTreeGraph] ⚠️ context.TreeGraphApi 为 null,未注入后端端点");
        }

        context.MafRagTreeGraph
            .WithHttpHealthCheck(
                path: "/",
                statusCode: 200,
                endpointName: "http");

        context.MafRagTreeGraph.WithOtlpExporter();

        return builder;
    }
}
