using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// MafSampleApi 的 Aspire 编排扩展。
/// 结构与 <see cref="MafWorkFlowExtension"/> 保持一致：
/// 固定端口 → 链式 WireIfPresent → 模型名注入 → 端点注入 → 外部资源 → 健康检查 → OTLP。
/// </summary>
public static class MafSampleApiExtension
{
    private const int MafSampleApiHttpPort = 5324;

    public static void AddMafSampleApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        // ─── 项目声明与固定端口 ────────────────────────────────────
        context.MafSampleApi = builder
            .AddProject<Projects.MafSampleApi>("MafSampleApi")
            .WithHttpEndpoint(port: MafSampleApiHttpPort, name: "http");

        // ─── 内部资源：链式 WireIfPresent ──────────────────────────
        // MAF Chat/SSE 场景只需要 Redis（会话持久化可选）与 Ollama。
        // ChatModel / Embedding 作为模型资源本身不直接 Wire，
        // 而是等待 Ollama 容器就绪后由 Ollama 内部拉起模型。
        context.MafSampleApi
            .WireIfPresent(context.Redis)
            .WireIfPresent(context.Ollama)
            .WireIfPresent(context.ChatModel, waitFor: false)
            .WireIfPresent(context.Embedding, waitFor: false)
            .WireIfPresent(context.LiteGraph);

        // ══════════════════════════════════════════════════════════
        // ★ 模型名注入（引用常量，不依赖 AddOllama 的调用顺序）
        //   配置键与 AgentOptions 一一对应（AgentOptions.SectionName = "Agent"）
        // ══════════════════════════════════════════════════════════
        context.MafSampleApi
            .WithEnvironment("Agent__ChatModel",      OllamaExtension.ChatModelName)
            .WithEnvironment("Agent__EmbeddingModel", OllamaExtension.EmbeddingModelName)
            .WithEnvironment("Agent__ModelId",        OllamaExtension.ChatModelName)
            .WithEnvironment("Agent__MaxSessions",    "256")
            .WithEnvironment("Agent__SessionIdleTimeout", "00:30:00");

        // ══════════════════════════════════════════════════════════
        // ★ Ollama 端点显式注入
        //   Program.cs 读取 OLLAMA_ENDPOINT（优先级最高），
        //   兜底读 Ollama:Endpoint。两条路径都铺好，避免容器 DNS 解析差异。
        // ══════════════════════════════════════════════════════════
        if (context.Ollama is not null)
        {
            var ollamaEndpoint = context.Ollama.GetEndpoint(OllamaExtension.HttpEndpointName);
            context.MafSampleApi
                .WithEnvironment("OLLAMA_ENDPOINT", ollamaEndpoint)
                .WithEnvironment("Ollama__Endpoint", ollamaEndpoint);
        }

        // ══════════════════════════════════════════════════════════
        // ★ LiteGraph 端点注入（与 MafWorkFlowApi 保持一致的键名）
        // ══════════════════════════════════════════════════════════
        if (context.LiteGraph is not null)
        {
            context.MafSampleApi
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
        var logger = loggerFactory.CreateLogger("MafSampleApiExtension");

        ExternalServiceBinding[] externalBindings =
        [
            ExternalServiceBinding.From(
                context.SeaweedS3, endpointName: "s3",
                connectionStringName: "seaweedfs"),
        ];

        foreach (var binding in externalBindings)
            binding.Apply(builder, context.MafSampleApi, logger);

        // ─── 健康检查 ─────────────────────────────────────────────
        context.MafSampleApi
            .WithHttpHealthCheck(
                path: "/api/health",
                statusCode: 200,
                endpointName: "http");

        context.MafSampleApi.WithOtlpExporter();
    }
}