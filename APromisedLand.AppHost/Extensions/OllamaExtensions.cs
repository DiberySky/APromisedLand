namespace APromisedLand.AppHost.Extensions;

public static class OllamaExtensions
{
    public static IDistributedApplicationBuilder AddOllama(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.Ollama = builder.AddOllama("Ollama", port: 11434)
            .WithDataVolume("ollama-data")
            .WithGPUSupport()
            .WithContainerRuntimeArgs("--gpus=all")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithOtlpExporter();

        context.Embedding = context.Ollama.AddModel("bge-large");

        context.OllamaService = builder.AddProject<Projects.OllamaService>("Ollama-Service")
            .WithReference(context.Embedding)
            .WaitFor(context.Embedding)
            .WithOtlpExporter();

        return builder;
    }
}