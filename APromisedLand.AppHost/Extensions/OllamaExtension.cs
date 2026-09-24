namespace APromisedLand.AppHost.Extensions;

public static class OllamaExtension
{
    public const string EmbeddingModelName = "bge-m3";
    public const string ChatModelName = "qwen3:8b";

    public static IDistributedApplicationBuilder AddOllama(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Ollama = builder.AddOllama("ollama")
            .WithDataVolume("ollama-data")
            .WithGPUSupport()
            .WithLifetime(ContainerLifetime.Persistent);

        resourceContext.Embedding = resourceContext.Ollama
            .AddModel("embedding", EmbeddingModelName);

        resourceContext.ChatModel = resourceContext.Ollama
            .AddModel("chat-model", ChatModelName);

        // ★ Reranker 走 Python Cross-Encoder + LLM 打分兜底，
        //   不需要 Ollama 原生 rerank 模型

        resourceContext.EmbeddingModelName = EmbeddingModelName;
        resourceContext.ChatModelName = ChatModelName;

        return builder;
    }
}