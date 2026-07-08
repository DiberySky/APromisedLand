namespace APromisedLand.AppHost.Extensions;

public static class QuestionTypesenseExtensions
{
    public static IDistributedApplicationBuilder AddQustionTypesense(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.RabbitMq is null || context.TypesenseEndpoint is null ||
            context.Typesense is null || context.TypesenseApiKey is null) return builder;

        // Typesense-Service
        context.TypesenseService = builder.AddProject<Projects.SearchService>("Typesense-question")
            .WithEnvironment("typesense-api-key", context.TypesenseApiKey)
            .WithReference(context.TypesenseEndpoint)
            .WithReference(context.RabbitMq)
            .WaitFor(context.Typesense)
            .WaitFor(context.RabbitMq);

        // if (context.TypesenseEndpoint is null || context.Typesense is null || 
        //     context.TypesenseApiKey is null || context.Nats is null) return builder;
        //
        // context.TypesenseService = builder.AddProject<Projects.QuestionTypesenseService>("Typesense-question")
        //     .WithEnvironment("typesense-api-key", context.TypesenseApiKey)
        //     .WithReference(context.TypesenseEndpoint)
        //     .WithReference(context.Nats)
        //     .WaitFor(context.Typesense)
        //     .WaitFor(context.Nats);
        
        return builder;
    }
}