namespace APromisedLand.AppHost.Extensions;

public static class OllamaExtension
{
    // ══════════════════════════════════════════════════════════
    // ★ 唯一真相源：想换模型只改这里
    // ══════════════════════════════════════════════════════════

    /// <summary>嵌入模型名。中文语义建议 "bge-m3"，英文可用 "bge-large"。</summary>
    public const string EmbeddingModelName = "bge-m3";

    /// <summary>意图识别用的 chat 模型。</summary>
    public const string ChatModelName = "qwen3:8b";

    /// <summary>Reranker 专用的打分模型（无 thinking，速度更快）。</summary>
    public const string RerankerChatModelName = "qwen2.5:7b";

    /// <summary>Reranker 模型名（作为 /api/rerank 原生端点用）。</summary>
    public const string RerankerModelName = "awenleven/bge-reranker-v2-m3";

    public static IDistributedApplicationBuilder AddOllama(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Ollama = builder.AddOllama("ollama")
            .WithDataVolume("ollama-data")
            .WithGPUSupport()
            .WithLifetime(ContainerLifetime.Persistent);

        // ── 拉取模型 ──
        resourceContext.Embedding = resourceContext.Ollama
            .AddModel("embedding", EmbeddingModelName);

        resourceContext.ChatModel = resourceContext.Ollama
            .AddModel("chat-model", ChatModelName);

        resourceContext.Reranker = resourceContext.Ollama
            .AddModel("reranker-model", RerankerModelName);

        // ★ Reranker 打分模型（qwen2.5:7b）
        resourceContext.Reranker = resourceContext.Ollama
            .AddModel("reranker-chat-model", RerankerChatModelName);

        // ── 写入 context，供 MafWorkFlowExtension 注入 ──
        resourceContext.EmbeddingModelName = EmbeddingModelName;
        resourceContext.ChatModelName = ChatModelName;
        resourceContext.RerankerName = RerankerModelName;
        resourceContext.RerankerChatModelName = RerankerChatModelName;   // ★ 新增

        return builder;
    }
}