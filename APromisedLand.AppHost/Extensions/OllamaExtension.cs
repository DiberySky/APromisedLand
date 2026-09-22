namespace APromisedLand.AppHost.Extensions;

public static class OllamaExtension
{
    // ══════════════════════════════════════════════════════════
    // ★ 唯一真相源：想换模型只改这两行
    // ══════════════════════════════════════════════════════════
    /// <summary>Ollama 嵌入模型名。中文语义建议 "bge-m3"，英文可用 "bge-large"。</summary>
    public const string EmbeddingModelName = "bge-m3";

    /// <summary>Ollama 聊天模型名。</summary>
    public const string ChatModelName = "qwen3:8b";

    public static IDistributedApplicationBuilder AddOllama(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // WithGPUSupport() 内部已包含 --gpus=all，无需重复传递。
        resourceContext.Ollama = builder.AddOllama("ollama")
            .WithDataVolume("ollama-data")
            .WithGPUSupport()
            .WithLifetime(ContainerLifetime.Persistent);

        // 嵌入模型：资源名 = "embedding"，实际拉取的模型标签 = EmbeddingModelName
        resourceContext.Embedding = resourceContext.Ollama
            .AddModel("embedding", EmbeddingModelName);

        // 聊天模型：资源名 = "chat-model"，实际拉取的模型标签 = ChatModelName
        resourceContext.ChatModel = resourceContext.Ollama
            .AddModel("chat-model", ChatModelName);

        // ★ 把模型名写入 context，供 MafWorkFlowExtension 注入给 MAFWorkFlowApi
        resourceContext.EmbeddingModelName = EmbeddingModelName;
        resourceContext.ChatModelName = ChatModelName;

        return builder;
    }
}