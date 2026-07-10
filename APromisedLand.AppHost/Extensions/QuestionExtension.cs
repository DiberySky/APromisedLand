namespace APromisedLand.AppHost.Extensions;

public static class QuestionExtension
{
    public static IDistributedApplicationBuilder AddQuestionService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.QuestionDb is null || context.Keycloak is null ||
            context.Redis is null || context.Ollama is null ||
            context.Nats is null || context.Elasticsearch is null ||
            context.TypesenseEndpoint is null) return builder;
        
        // QuestionService
        context.QuestionService = builder.AddProject<Projects.QuestionService>("Question-Service")
            .WithEnvironment("typesense-api-key", context.TypesenseApiKey)
            .WithReference(context.TypesenseEndpoint)
            .WithReference(context.Keycloak)
            .WithReference(context.QuestionDb)
            // .WithReference(context.RabbitMq)
            .WithReference(context.Redis)
            .WithReference(context.Elasticsearch)
            .WithReference(context.Ollama)
            .WithReference(context.Nats)
            .WaitFor(context.Keycloak)
            .WaitFor(context.QuestionDb)
            // .WaitFor(context.RabbitMq)
            .WaitFor(context.Elasticsearch)
            .WaitFor(context.Ollama)
            .WaitFor(context.Nats)
            .WaitFor(context.Redis);

        // WeatherApi
        context.WeatherApi = builder.AddProject<Projects.WeatherApi>("Weather-Api")
            .WithReference(context.Keycloak)
            .WaitFor(context.Keycloak);

        return builder;
    }
}