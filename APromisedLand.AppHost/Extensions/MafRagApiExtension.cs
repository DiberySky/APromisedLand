using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// MafRagApi 的 Aspire 编排扩展。
/// ★ 后端已从 Ollama 迁移到 vLLM：
///   - Chat:      vllm 容器       (Qwen3-4B-AWQ)
///   - Embedding: vllm-embed 容器 (BAAI/bge-m3)
/// </summary>
public static class MafRagApiExtension
{
    private const int MafSampleApiHttpPort = 5737;

    public static void AddMafSampleApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        // ─── 项目声明与固定端口 ────────────────────────────────────
        context.MafSampleApi = builder
            .AddProject<Projects.MafRagApi>("MafRagApi")
            .WithHttpEndpoint(port: MafSampleApiHttpPort, name: "http");

        // ─── 内部资源：链式 WireIfPresent ──────────────────────────
        // 仅 Redis / LiteGraph 这类实现 IResourceWithConnectionString 的资源
        // 才能走 WireIfPresent。
        // vLLM 是 ContainerResource，没有连接字符串概念 → 下方用 WaitFor + 环境变量。
        context.MafSampleApi
            .WireIfPresent(context.Redis)
            .WireIfPresent(context.LiteGraph);

        // ══════════════════════════════════════════════════════════
        // ★ 模型名注入（引用 VllmExtension 常量，与容器 --served-model-name 一致）
        //   配置键与 AgentOptions 一一对应（AgentOptions.SectionName = "Agent"）
        // ══════════════════════════════════════════════════════════
        context.MafSampleApi
            .WithEnvironment("Agent__ChatModel",           VllmExtension.ServedModelName)
            .WithEnvironment("Agent__MaxSessions",         "256")
            .WithEnvironment("Agent__SessionIdleTimeout",  "00:30:00")
            .WithEnvironment("Agent__ChatBudgetSeconds",   "900")
            .WithEnvironment("Agent__LoopBudgetSeconds",   "1800");

        // ══════════════════════════════════════════════════════════
        // ★ vLLM Chat 容器：WaitFor + 显式端点注入
        //   ContainerResource 不实现 IResourceWithConnectionString，
        //   不能用 WireIfPresent / WithReference，只能 WaitFor 等健康检查通过，
        //   然后手动把 Endpoint 写进环境变量。
        //   Program.cs 优先级：VLLM_HTTP > Agent:Endpoint > localhost:8000
        // ══════════════════════════════════════════════════════════
        if (context.Vllm is not null)
        {
            var chatEndpoint = context.Vllm.GetEndpoint(VllmExtension.HttpEndpointName);

            context.MafSampleApi
                .WaitFor(context.Vllm)                  // ← 等 /health 探活通过
                .WithEnvironment("VLLM_HTTP",       chatEndpoint)
                .WithEnvironment("Agent__Endpoint", chatEndpoint);

            Console.WriteLine($"[MafRagApi] Chat 端点 → {chatEndpoint}");
        }
        else
        {
            Console.WriteLine("[MafRagApi] ⚠️ context.Vllm 为 null，未注入 Chat 端点");
        }

        // ══════════════════════════════════════════════════════════
        // ★ MafVectorSearchApi 端点注入
        //   MafRagApi 通过 VectorSearchClient 远程调用向量搜索服务。
        //   Program.cs 优先级：VECTOR_SEARCH__BASEURL > VectorSearch:BaseUrl > localhost:5741
        // ══════════════════════════════════════════════════════════
        if (context.MafVectorSearchApi is not null)
        {
            var vectorSearchEndpoint = context.MafVectorSearchApi.GetEndpoint("http");

            context.MafSampleApi
                .WaitFor(context.MafVectorSearchApi)
                .WithEnvironment("VECTOR_SEARCH__BASEURL", vectorSearchEndpoint)
                .WithEnvironment("VectorSearch__BaseUrl",  vectorSearchEndpoint);

            Console.WriteLine($"[MafRagApi] VectorSearch 端点 → {vectorSearchEndpoint}");
        }
        else
        {
            Console.WriteLine("[MafRagApi] ⚠️ context.MafVectorSearchApi 为 null，未注入向量搜索端点");
        }

        // ══════════════════════════════════════════════════════════
        // ★ LiteGraph 端点注入
        // ══════════════════════════════════════════════════════════
        if (context.LiteGraph is not null)
        {
            context.MafSampleApi
                .WithEnvironment(
                    "LiteGraph__Endpoint",
                    context.LiteGraph.GetEndpoint(LiteGraphResource.HttpEndpointName));
        }

        // ─── 健康检查 ─────────────────────────────────────────────
        // 轻量端点 /api/health，永远 200，供 liveness probe 使用。
        // 深度端点 /api/health/deep 会探活 vLLM，供 readiness 使用。
        context.MafSampleApi
            .WithHttpHealthCheck(
                path: "/api/health",
                statusCode: 200,
                endpointName: "http");

        context.MafSampleApi.WithOtlpExporter();
    }
}