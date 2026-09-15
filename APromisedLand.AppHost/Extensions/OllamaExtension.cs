namespace APromisedLand.AppHost.Extensions;

public static class OllamaExtension
{
    public static IDistributedApplicationBuilder AddOllama(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // WithGPUSupport() 内部已包含 --gpus=all，无需重复传递。
        resourceContext.Ollama = builder.AddOllama("Ollama")
            .WithDataVolume("ollama-data")
            .WithGPUSupport()
            .WithLifetime(ContainerLifetime.Persistent);

        // 嵌入模型：资源名 = "embedding"，实际拉取的模型标签 = "bge-large"
        resourceContext.Embedding = resourceContext.Ollama.AddModel("embedding", "bge-large");

        // 聊天模型：资源名 = "chat-model"，实际拉取的模型标签 = "qwen2.5:7b" qwen3.8:27b
        resourceContext.ChatModel = resourceContext.Ollama.AddModel("chat-model", "qwen2.5:7b");

        return builder;
    }
}