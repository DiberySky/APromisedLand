namespace APromisedLand.AppHost.Extensions;

public static class SemanticSearchExtension
{
    public static IDistributedApplicationBuilder AddSemanticSearch(
        this IDistributedApplicationBuilder builder, AppHostResourceContext resourceContext)
    {
        if (resourceContext.Elasticsearch is null || resourceContext.Ollama is null || 
            resourceContext.Nats is null) return builder;

        var semantic = builder.AddProject<Projects.SemanticSearch_Api>("SemanticSearch-Service")
            .WithReference(resourceContext.Elasticsearch)
            .WithReference(resourceContext.Ollama)
            .WithReference(resourceContext.Nats)
            .WaitFor(resourceContext.Elasticsearch)
            .WaitFor(resourceContext.Ollama)
            .WaitFor(resourceContext.Nats);
        
        return builder;
    }
}