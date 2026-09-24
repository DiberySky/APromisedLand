using System.Diagnostics.CodeAnalysis;

namespace APromisedLand.AppHost.Extensions;

public static class RerankerExtension
{
    private const int RerankerHttpPort = 5919;

    [SuppressMessage("ReSharper", "SpellingError")]
    public static void AddRerankerService(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        var reranker = builder
            .AddPythonApp("reranker", "../../RerankerService", "reranker_server.py")
            .WithVirtualEnvironment(".venv")
            // ══════════════════════════════════════════════════════
            // ★ 关键：isProxied: false
            //   非容器资源 + Port == TargetPort → 必须关代理
            //   否则 Aspire 报 "Non-container resources cannot be proxied"
            // ══════════════════════════════════════════════════════
            .WithHttpEndpoint(
                port: RerankerHttpPort,           // 外部端口 5919
                targetPort: RerankerHttpPort,     // 内部端口 5919（Python 真实监听）
                name: "http",
                env: "PORT",
                isProxied: false)                 // ★ 关键
            .WithEnvironment("RERANKER_MODEL", "BAAI/bge-reranker-v2-m3")
            .WithEnvironment("RERANKER_BATCH_SIZE", "32")
            .WithEnvironment("RERANKER_MAX_LENGTH", "512")
            .WithEnvironment("HF_ENDPOINT", "https://hf-mirror.com");

        context.RerankerService = reranker;

        reranker.WithHttpHealthCheck(
            path: "/health",
            statusCode: 200,
            endpointName: "http");

        if (context.MafWorkFlowApi is not null)
        {
            context.MafWorkFlowApi
                .WithEnvironment("Reranker__Endpoint", reranker.GetEndpoint("http"))
                .WaitFor(reranker);
        }
    }
}