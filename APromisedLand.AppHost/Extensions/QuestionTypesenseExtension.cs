namespace APromisedLand.AppHost.Extensions;

public static class QuestionTypesenseExtension
{
    public static IDistributedApplicationBuilder AddQustionTypesense(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        if (resourceContext.RabbitMq is null || resourceContext.TypesenseEndpoint is null ||
            resourceContext.Typesense is null || resourceContext.TypesenseApiKey is null) return builder;

        // Typesense-Service
        resourceContext.TypesenseService = builder.AddProject<Projects.SearchService>("Typesense-question")
            .WithEnvironment("typesense-api-key", resourceContext.TypesenseApiKey)
            .WithReference(resourceContext.TypesenseEndpoint)
            .WithReference(resourceContext.RabbitMq)
            .WaitFor(resourceContext.Typesense)
            .WaitFor(resourceContext.RabbitMq);

        // if (resourceContext.TypesenseEndpoint is null || resourceContext.Typesense is null || 
        //     resourceContext.TypesenseApiKey is null || resourceContext.Nats is null) return builder;
        //
        // resourceContext.TypesenseService = builder.AddProject<Projects.QuestionTypesenseService>("Typesense-question")
        //     .WithEnvironment("typesense-api-key", resourceContext.TypesenseApiKey)
        //     .WithReference(resourceContext.TypesenseEndpoint)
        //     .WithReference(resourceContext.Nats)
        //     .WaitFor(resourceContext.Typesense)
        //     .WaitFor(resourceContext.Nats);
        
        return builder;
    }
}