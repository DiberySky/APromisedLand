using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace APromisedLand.AppHost.Extensions;

public static class MafWorkFlowExtension
{
    private const int MafWorkFlowHttpPort = 5323;

    public static void AddMafWorkFlowApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        // ─── 项目声明与固定端口 ────────────────────────────────────
        context.MafWorkFlowApi = builder
            .AddProject<Projects.MAFWorkFlowApi>("MAFWorkFlowApi")
            .WithHttpEndpoint(port: MafWorkFlowHttpPort, name: "http");

        // ─── 内部资源：链式 WireIfPresent ──────────────────────────
        context.MafWorkFlowApi
            .WireIfPresent(context.Redis)
            .WireIfPresent(context.FileMetadataDb)
            .WireIfPresent(context.HangfireDb)
            .WireIfPresent(context.Ollama)
            .WireIfPresent(context.ChatModel, waitFor: false)
            .WireIfPresent(context.Embedding, waitFor: false)
            .WireIfPresent(context.LiteGraph);

        // ══════════════════════════════════════════════════════════
        // ★ 模型名注入（引用常量，不依赖 AddOllama 的调用顺序）
        // ══════════════════════════════════════════════════════════
        context.MafWorkFlowApi
            .WithEnvironment("Embedding__Model",     OllamaExtension.EmbeddingModelName)
            .WithEnvironment("Chat__Model",          OllamaExtension.ChatModelName)
            .WithEnvironment("Reranker__ChatModel",  OllamaExtension.ChatModelName)
            // ★ 新增：Agent__ModelId —— 供 OllamaWarmupService / 健康检查使用
            .WithEnvironment("Agent__ModelId",       OllamaExtension.ChatModelName);

        // ══════════════════════════════════════════════════════════
        // ★ LiteGraph 端点注入（走服务发现，覆盖 appsettings）
        // ══════════════════════════════════════════════════════════
        if (context.LiteGraph is not null)
        {
            context.MafWorkFlowApi
                .WithEnvironment(
                    "LiteGraph__Endpoint",
                    context.LiteGraph.GetEndpoint(LiteGraphResource.HttpEndpointName));
        }

        // ─── 外部资源 ─────────────────────────────────────────────
        using var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddSimpleConsole(o => o.SingleLine = true);
            logging.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger("MafWorkFlowExtension");

        ExternalServiceBinding[] externalBindings =
        [
            ExternalServiceBinding.From(
                context.SeaweedS3, endpointName: "s3",
                connectionStringName: "seaweedfs"),
        ];

        foreach (var binding in externalBindings)
            binding.Apply(builder, context.MafWorkFlowApi, logger);

        // ─── 健康检查 ─────────────────────────────────────────────
        context.MafWorkFlowApi
            .WithHttpHealthCheck(
                path: "/health",
                statusCode: 200,
                endpointName: "http");

        context.MafWorkFlowApi.WithOtlpExporter();
    }
}