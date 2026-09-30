using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// MafVectorSearchApi 的 Aspire 编排扩展。
/// 职责：文本向量化（vLLM bge-m3）、文档摄入与分块、语义检索（向量 + reranker + BM25 降级）、文档重排序。
/// 依赖：vLLM Embedding 容器 + Reranker 服务。
/// </summary>
public static class MafVectorSearchApiExtension
{
    /// <summary>MafVectorSearchApi 固定宿主端口。</summary>
    public const int VectorSearchHttpPort = 5741;

    public static void AddMafVectorSearchApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        // ─── 项目声明与固定端口 ────────────────────────────────────
        context.MafVectorSearchApi = builder
            .AddProject<Projects.MafVectorSearchApi>("MafVectorSearchApi")
            .WithHttpEndpoint(port: VectorSearchHttpPort, name: "http");

        // ══════════════════════════════════════════════════════════
        // ★ Embedding 配置注入（引用 VllmExtension 常量）
        //   配置键与 EmbeddingOptions 一一对应（EmbeddingOptions.SectionName = "Embedding"）
        // ══════════════════════════════════════════════════════════
        context.MafVectorSearchApi
            .WithEnvironment("Embedding__Model",     VllmExtension.ServedEmbeddingModelName)
            .WithEnvironment("Embedding__Dimension", VllmExtension.EmbeddingDimension.ToString());

        // ══════════════════════════════════════════════════════════
        // ★ vLLM Embedding 容器：WaitFor + 显式端点注入
        //   Program.cs 优先级：VLLM_EMBEDDING_HTTP > Embedding:Endpoint > localhost:5719
        // ══════════════════════════════════════════════════════════
        if (context.VllmEmbed is not null)
        {
            var embedEndpoint = context.VllmEmbed.GetEndpoint(
                VllmExtension.EmbeddingHttpEndpointName);

            context.MafVectorSearchApi
                .WaitFor(context.VllmEmbed)
                .WithEnvironment("VLLM_EMBEDDING_HTTP",   embedEndpoint)
                .WithEnvironment("Embedding__Endpoint",   embedEndpoint);

            Console.WriteLine($"[MafVectorSearchApi] Embedding 端点 → {embedEndpoint}");
        }
        else
        {
            Console.WriteLine("[MafVectorSearchApi] ⚠️ context.VllmEmbed 为 null，未注入 Embedding 端点");
        }

        // ══════════════════════════════════════════════════════════
        // ★ Reranker 端点注入
        // ══════════════════════════════════════════════════════════
        if (context.RerankerService is not null)
        {
            var rerankerEndpoint = context.RerankerService.GetEndpoint("http");

            context.MafVectorSearchApi
                .WithEnvironment("Reranker__Endpoint", rerankerEndpoint)
                .WaitFor(context.RerankerService);

            Console.WriteLine($"[MafVectorSearchApi] Reranker 端点 → {rerankerEndpoint}");
        }
        else
        {
            Console.WriteLine("[MafVectorSearchApi] ⚠️ context.RerankerService 为 null，未注入 Reranker 端点");
        }

        // ─── 健康检查 ─────────────────────────────────────────────
        context.MafVectorSearchApi
            .WithHttpHealthCheck(
                path: "/api/rag/stats",
                statusCode: 200,
                endpointName: "http");

        context.MafVectorSearchApi.WithOtlpExporter();
    }
}
