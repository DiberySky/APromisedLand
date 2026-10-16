using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraph.MafWfApi 的 Aspire 编排扩展。
/// ★ MAF + Workflow 服务 API 的 vLLM 版（替代旧 MAFWorkFlowApi，复用端口 5323）：
///   - Chat:      vllm 容器       (Qwen3-4B-AWQ)
///   - Embedding: vllm-embed 容器 (BAAI/bge-m3)
/// 集成模式与 MafRagApiExtension 一致：
///   vLLM 是 ContainerResource，不能用 WireIfPresent / WithReference，
///   只能 WaitFor 等容器 Running，再把端点写进环境变量；
///   模型实际就绪由 TreeGraph.MafWfApi 的 VllmWarmupService 预热保证。
/// </summary>
public static class TreeGraphMafWfExtension
{
    private const int HttpPort = 5323;

    public static void AddTreeGraphMafWfApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        // ─── 项目声明与固定端口 ────────────────────────────────────
        context.TreeGraphMafWfApi = builder
            .AddProject<Projects.TreeGraph_MafWfApi>("TreeGraphMafWfApi")
            .WithHttpEndpoint(port: HttpPort, name: "http");

        // ─── 内部资源：链式 WireIfPresent ──────────────────────────
        context.TreeGraphMafWfApi
            .WireIfPresent(context.Redis)
            .WireIfPresent(context.LiteGraph);

        // ══════════════════════════════════════════════════════════
        // ★ 模型名注入（引用 VllmExtension 常量，与容器 --served-model-name 一致）
        // ══════════════════════════════════════════════════════════
        context.TreeGraphMafWfApi
            .WithEnvironment("Agent__ChatModel", VllmExtension.ServedModelName);

        // ══════════════════════════════════════════════════════════
        // ★ vLLM Chat 容器：WaitFor + 显式端点注入
        //   Program.cs 优先级：VLLM_HTTP > Agent:Endpoint > localhost:8000
        // ══════════════════════════════════════════════════════════
        if (context.Vllm is not null)
        {
            var chatEndpoint = context.Vllm.GetEndpoint(VllmExtension.HttpEndpointName);

            context.TreeGraphMafWfApi
                .WaitFor(context.Vllm)                  // ← 等容器 Running（模型就绪由 VllmWarmupService 保证）
                .WithEnvironment("VLLM_HTTP",       chatEndpoint)
                .WithEnvironment("Agent__Endpoint", chatEndpoint);

            Console.WriteLine($"[TreeGraphMafWfApi] Chat 端点 → {chatEndpoint}");
        }
        else
        {
            Console.WriteLine("[TreeGraphMafWfApi] ⚠️ context.Vllm 为 null，未注入 Chat 端点");
        }

        // ══════════════════════════════════════════════════════════
        // ★ vLLM Embedding 容器：WaitFor + 端点/模型名注入（预留）
        // ══════════════════════════════════════════════════════════
        if (context.VllmEmbed is not null)
        {
            var embedEndpoint = context.VllmEmbed.GetEndpoint(VllmExtension.EmbeddingHttpEndpointName);

            context.TreeGraphMafWfApi
                .WaitFor(context.VllmEmbed)
                .WithEnvironment("VLLM_EMBEDDING_HTTP",   embedEndpoint)
                .WithEnvironment("Embedding__Endpoint",   embedEndpoint)
                .WithEnvironment("Embedding__Model",      VllmExtension.ServedEmbeddingModelName)
                .WithEnvironment("Embedding__Dimension",  VllmExtension.EmbeddingDimension.ToString());

            Console.WriteLine($"[TreeGraphMafWfApi] Embedding 端点 → {embedEndpoint}");
        }

        // ══════════════════════════════════════════════════════════
        // ★ LiteGraph 端点注入
        // ══════════════════════════════════════════════════════════
        if (context.LiteGraph is not null)
        {
            context.TreeGraphMafWfApi
                .WithEnvironment(
                    "LiteGraph__Endpoint",
                    context.LiteGraph.GetEndpoint(LiteGraphResource.HttpEndpointName));
        }

        // ─── 健康检查 ─────────────────────────────────────────────
        // 轻量端点 /api/health，永远 200，供 liveness probe 使用。
        // 深度端点 /api/health/deep 会探活 vLLM，供 readiness 使用。
        context.TreeGraphMafWfApi
            .WithHttpHealthCheck(
                path: "/api/health",
                statusCode: 200,
                endpointName: "http");

        context.TreeGraphMafWfApi.WithOtlpExporter();
    }
}
