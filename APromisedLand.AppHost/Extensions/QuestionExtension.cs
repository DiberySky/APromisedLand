namespace APromisedLand.AppHost.Extensions;

public static class QuestionExtension
{
    public static IDistributedApplicationBuilder AddQuestionService(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.QuestionService = builder.AddProject<Projects.QuestionService>("Question-Service");
        
        if (resourceContext.QuestionDb != null )
        {
            resourceContext.QuestionService.WithReference(resourceContext.QuestionDb);
            resourceContext.QuestionService.WaitFor(resourceContext.QuestionDb);
        }
        
        if (resourceContext.Keycloak != null )
        {
            resourceContext.QuestionService.WithReference(resourceContext.Keycloak);
            resourceContext.QuestionService.WaitFor(resourceContext.Keycloak);
        }
        
        if (resourceContext.Redis != null )
        {
            resourceContext.QuestionService.WithReference(resourceContext.Redis);
            resourceContext.QuestionService.WaitFor(resourceContext.Redis);
        }
        
        if (resourceContext.Ollama != null )
        {
            resourceContext.QuestionService.WithReference(resourceContext.Ollama);
            resourceContext.QuestionService.WaitFor(resourceContext.Ollama);
        }
        
        if (resourceContext.Nats != null )
        {
            resourceContext.QuestionService.WithReference(resourceContext.Nats);
            resourceContext.QuestionService.WaitFor(resourceContext.Nats);
        }
        
        if (resourceContext.Elasticsearch != null )
        {
            resourceContext.QuestionService.WithReference(resourceContext.Elasticsearch);
            resourceContext.QuestionService.WaitFor(resourceContext.Elasticsearch);
        }
        
        if (resourceContext.TypesenseEndpoint != null )
        {
            resourceContext.QuestionService.WithReference(resourceContext.TypesenseEndpoint)
                .WithEnvironment("typesense-api-key", resourceContext.TypesenseApiKey)
                .WithReference(resourceContext.TypesenseEndpoint);
        }
        
        if (resourceContext.RabbitMq != null )
        {
            resourceContext.QuestionService.WithReference(resourceContext.RabbitMq);
            resourceContext.QuestionService.WaitFor(resourceContext.RabbitMq);
        }
        
        // if (resourceContext.QuestionDb is null || resourceContext.Keycloak is null ||
        //     resourceContext.Redis is null || resourceContext.Ollama is null ||
        //     resourceContext.Nats is null || resourceContext.Elasticsearch is null ||
        //     resourceContext.TypesenseEndpoint is null) return builder;
        //
        // // QuestionService
        // resourceContext.QuestionService = builder.AddProject<Projects.QuestionService>("Question-Service")
        //     .WithEnvironment("typesense-api-key", resourceContext.TypesenseApiKey)
        //     .WithReference(resourceContext.TypesenseEndpoint)
        //     .WithReference(resourceContext.Keycloak)
        //     .WithReference(resourceContext.QuestionDb)
        //     .WithReference(resourceContext.RabbitMq)
        //     .WithReference(resourceContext.Redis)
        //     .WithReference(resourceContext.Elasticsearch)
        //     .WithReference(resourceContext.Ollama)
        //     .WithReference(resourceContext.Nats)
        //     .WaitFor(resourceContext.Keycloak)
        //     .WaitFor(resourceContext.QuestionDb)
        //     .WaitFor(resourceContext.RabbitMq)
        //     .WaitFor(resourceContext.Elasticsearch)
        //     .WaitFor(resourceContext.Ollama)
        //     .WaitFor(resourceContext.Nats)
        //     .WaitFor(resourceContext.Redis);

        // WeatherApi
        // resourceContext.WeatherApi = builder.AddProject<Projects.WeatherApi>("Weather-Api");
        //
        // if (resourceContext.Keycloak is not null)
        // {
        //     resourceContext.QuestionService.WithReference(resourceContext.Keycloak)
        //     .WaitFor(resourceContext.Keycloak);
        // }

        return builder;
    }
}