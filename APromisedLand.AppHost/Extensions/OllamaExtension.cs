namespace APromisedLand.AppHost.Extensions;

public static class OllamaExtension
{
    public static IDistributedApplicationBuilder AddOllama(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.Ollama = builder.AddOllama("Ollama")
            .WithDataVolume("ollama-data")
            .WithGPUSupport()
            .WithContainerRuntimeArgs("--gpus=all")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithOtlpExporter();

        context.Embedding = context.Ollama.AddModel("bge-large");
        
        // Add a model to Ollama (default: llama3.2:1b)
        context.AIModel = context.Ollama.AddModel("chat-model", "qwen2.5:7b"); //llama3.2:1b

        // context.OllamaService = builder.AddProject<Projects.OllamaService>("Ollama-Service")
        //     .WithReference(context.Embedding)
        //     .WaitFor(context.Embedding)
        //     .WithOtlpExporter();

        return builder;
    }
}