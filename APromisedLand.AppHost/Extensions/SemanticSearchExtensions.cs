namespace APromisedLand.AppHost.Extensions;

public static class SemanticSearchExtensions
{
    public static IDistributedApplicationBuilder AddSemanticSearch(
        this IDistributedApplicationBuilder builder, AppHostContext context)
    {
        if (context.Elasticsearch is null || context.Ollama is null || 
            context.Nats is null) return builder;

        var semantic = builder.AddProject<Projects.SemanticSearch_Api>("SemanticSearch-Service")
            .WithReference(context.Elasticsearch)
            .WithReference(context.Ollama)
            .WithReference(context.Nats)
            .WaitFor(context.Elasticsearch)
            .WaitFor(context.Ollama)
            .WaitFor(context.Nats);
        
        return builder;
    }
}