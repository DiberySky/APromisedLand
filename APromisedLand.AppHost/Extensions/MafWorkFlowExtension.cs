using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace APromisedLand.AppHost.Extensions;

public static class MafWorkFlowExtension
{
    /// <summary>
    /// MafWorkFlowApi 固定的宿主机 HTTP 端口。
    /// </summary>
    private const int MafWorkFlowHttpPort = 5323;

    public static void AddMafWorkFlowApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        // ─── 项目声明与固定端口 ────────────────────────────────────
        context.MafWorkFlowApi = builder
            .AddProject<Projects.MAFWorkFlowApi>("MafWorkFlowApi")
            .WithHttpEndpoint(port: MafWorkFlowHttpPort, name: "http");

        // ===================================================================
        // 内部资源：链式 WireIfPresent
        //   - Redis：多轮会话持久化存储（新增关键依赖）
        //   - 数据层：强等待，确保存储就绪再启动 API
        //   - Ollama 容器：强等待
        //   - 模型资源：只注入引用、不 WaitFor，由 API 层健康检查兜底
        // ===================================================================
        context.MafWorkFlowApi
            .WireIfPresent(context.Redis)              // ⭐ 会话持久化
            .WireIfPresent(context.MetadataDb)
            .WireIfPresent(context.HangfireDb)
            .WireIfPresent(context.Ollama)
            .WireIfPresent(context.ChatModel, waitFor: false)
            .WireIfPresent(context.Embedding, waitFor: false);

        // ===================================================================
        // 外部资源：清单式声明
        // ===================================================================
        var logger = builder.Services
            .BuildServiceProvider()
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MafWorkFlowExtension");

        ExternalServiceBinding[] externalBindings =
        [
            ExternalServiceBinding.From(
                context.NebulaGraph, endpointName: "graph",
                connectionStringName: "nebula", schemeOverride: "thrift"),

            ExternalServiceBinding.From(
                context.Weaviate, endpointName: "http",
                connectionStringName: "weaviate"),

            ExternalServiceBinding.From(
                context.SeaweedS3, endpointName: "s3",
                connectionStringName: "seaweedfs"),
        ];

        foreach (var binding in externalBindings)
            binding.Apply(builder, context.MafWorkFlowApi, logger);

        // ===================================================================
        // 将 API 的 /health 端点接入 Aspire 资源健康状态
        // ===================================================================
        context.MafWorkFlowApi
            .WithHttpHealthCheck(
                path: "/health",
                statusCode: 200,
                endpointName: "http");

        context.MafWorkFlowApi.WithOtlpExporter();
    }
}