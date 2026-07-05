namespace APromisedLand.AppHost.Extensions;

public static class OllamaTestingExtensions
{
    public static IDistributedApplicationBuilder AddOllamaTesting(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // context.Ollama = builder.AddOllama("Ollama", port: 11434)
        //     .WithDataVolume("ollama-data")
        //     .WithGPUSupport()
        //     .WithContainerRuntimeArgs("--gpus=all")
        //     .WithLifetime(ContainerLifetime.Persistent)
        //     .WithOtlpExporter();

        //context.Embedding = context.Ollama.AddModel("bge-large");

        if (context.Ollama is null || context.Embedding is null) return builder;
        
        context.OllamaService = builder.AddProject<Projects.OllamaService>("Ollama-testing")
            .WithReference(context.Embedding)
            .WaitFor(context.Embedding)
            .WithOtlpExporter();

        return builder;
    }
}