namespace APromisedLand.AppHost.Extensions;

public static class QuestionExtension
{
    public static IDistributedApplicationBuilder AddQuestionService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.QuestionService = builder.AddProject<Projects.QuestionService>("Question-Service");
        
        if (context.QuestionDb != null )
        {
            context.QuestionService.WithReference(context.QuestionDb);
            context.QuestionService.WaitFor(context.QuestionDb);
        }
        
        if (context.Keycloak != null )
        {
            context.QuestionService.WithReference(context.Keycloak);
            context.QuestionService.WaitFor(context.Keycloak);
        }
        
        if (context.Redis != null )
        {
            context.QuestionService.WithReference(context.Redis);
            context.QuestionService.WaitFor(context.Redis);
        }
        
        if (context.Ollama != null )
        {
            context.QuestionService.WithReference(context.Ollama);
            context.QuestionService.WaitFor(context.Ollama);
        }
        
        if (context.Nats != null )
        {
            context.QuestionService.WithReference(context.Nats);
            context.QuestionService.WaitFor(context.Nats);
        }
        
        if (context.Elasticsearch != null )
        {
            context.QuestionService.WithReference(context.Elasticsearch);
            context.QuestionService.WaitFor(context.Elasticsearch);
        }
        
        if (context.TypesenseEndpoint != null )
        {
            context.QuestionService.WithReference(context.TypesenseEndpoint)
                .WithEnvironment("typesense-api-key", context.TypesenseApiKey)
                .WithReference(context.TypesenseEndpoint);
        }
        
        if (context.RabbitMq != null )
        {
            context.QuestionService.WithReference(context.RabbitMq);
            context.QuestionService.WaitFor(context.RabbitMq);
        }
        
        // if (context.QuestionDb is null || context.Keycloak is null ||
        //     context.Redis is null || context.Ollama is null ||
        //     context.Nats is null || context.Elasticsearch is null ||
        //     context.TypesenseEndpoint is null) return builder;
        //
        // // QuestionService
        // context.QuestionService = builder.AddProject<Projects.QuestionService>("Question-Service")
        //     .WithEnvironment("typesense-api-key", context.TypesenseApiKey)
        //     .WithReference(context.TypesenseEndpoint)
        //     .WithReference(context.Keycloak)
        //     .WithReference(context.QuestionDb)
        //     .WithReference(context.RabbitMq)
        //     .WithReference(context.Redis)
        //     .WithReference(context.Elasticsearch)
        //     .WithReference(context.Ollama)
        //     .WithReference(context.Nats)
        //     .WaitFor(context.Keycloak)
        //     .WaitFor(context.QuestionDb)
        //     .WaitFor(context.RabbitMq)
        //     .WaitFor(context.Elasticsearch)
        //     .WaitFor(context.Ollama)
        //     .WaitFor(context.Nats)
        //     .WaitFor(context.Redis);

        // WeatherApi
        // context.WeatherApi = builder.AddProject<Projects.WeatherApi>("Weather-Api");
        //
        // if (context.Keycloak is not null)
        // {
        //     context.QuestionService.WithReference(context.Keycloak)
        //     .WaitFor(context.Keycloak);
        // }

        return builder;
    }
}