namespace APromisedLand.AppHost.Extensions;

public static class OllamaExtension
{
    public static IDistributedApplicationBuilder AddOllama(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Ollama = builder.AddOllama("Ollama")
            .WithDataVolume("ollama-data")
            .WithGPUSupport()
            .WithContainerRuntimeArgs("--gpus=all")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithOtlpExporter();

        // 嵌入模型：资源名 = "embedding"，实际拉取的模型标签 = "bge-large"
        resourceContext.Embedding = resourceContext.Ollama.AddModel("embedding", "bge-large");

        // 聊天模型：资源名 = "chat-model"，实际拉取的模型标签 = "qwen2.5:7b"
        // 备选：qwen2.5:0.5b / llama3.2:1b / qwen2.5:7b
        resourceContext.ChatModel = resourceContext.Ollama.AddModel("chat-model", "qwen2.5:7b");

        return builder;
    }
}