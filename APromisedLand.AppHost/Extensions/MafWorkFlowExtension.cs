using Aspire.Hosting; // ★ WithEnvironment 在此命名空间
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace APromisedLand.AppHost.Extensions;

public static class MafWorkFlowExtension
{
    /// <summary>MAFWorkFlowApi 固定的宿主机 HTTP 端口。</summary>
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
        // ★ 显式注入模型名
        //   "__" 是 ASP.NET Core 配置的层级分隔符，
        //   "Embedding__Model" 会映射到 Configuration["Embedding:Model"]
        // ══════════════════════════════════════════════════════════
        if (!string.IsNullOrWhiteSpace(context.EmbeddingModelName))
        {
            context.MafWorkFlowApi
                .WithEnvironment("Embedding__Model", context.EmbeddingModelName);
        }

        if (!string.IsNullOrWhiteSpace(context.ChatModelName))
        {
            context.MafWorkFlowApi
                .WithEnvironment("Chat__Model", context.ChatModelName);
        }

        // ★ Reranker 打分复用 chat 模型
        if (!string.IsNullOrWhiteSpace(context.ChatModelName))
            context.MafWorkFlowApi.WithEnvironment("Reranker__ChatModel", context.ChatModelName);
        
        // ─── 外部资源：清单式声明 ─────────────────────────────────
        using var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddSimpleConsole(o => o.SingleLine = true);
            logging.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger("MafWorkFlowExtension");

        ExternalServiceBinding[] externalBindings =
        [
            // ExternalServiceBinding.From(
            //     context.NebulaGraph, endpointName: "graph",
            //     connectionStringName: "nebula", schemeOverride: "thrift"),
            //
            // ExternalServiceBinding.From(
            //     context.Weaviate, endpointName: "http",
            //     connectionStringName: "weaviate"),

            ExternalServiceBinding.From(
                context.SeaweedS3, endpointName: "s3",
                connectionStringName: "seaweedfs"),
        ];

        foreach (var binding in externalBindings)
            binding.Apply(builder, context.MafWorkFlowApi, logger);

        // ─── 健康检查接入 Aspire ──────────────────────────────────
        context.MafWorkFlowApi
            .WithHttpHealthCheck(
                path: "/health",
                statusCode: 200,
                endpointName: "http");

        context.MafWorkFlowApi.WithOtlpExporter();
    }
}