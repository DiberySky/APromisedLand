namespace APromisedLand.AppHost.Extensions;

public static class QuestionElasticExtension
{
        public static IDistributedApplicationBuilder AddQuestionElastic(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        if (resourceContext.Elasticsearch is null || resourceContext.RabbitMq is null ||
            resourceContext.Ollama is null) return builder;
        
        // Elastic-Service（业务服务）
        resourceContext.ElasticService = builder.AddProject<Projects.ElasticsearchService>("Elastic-question")
            .WithReference(resourceContext.RabbitMq)
            .WithReference(resourceContext.Elasticsearch)
            .WithReference(resourceContext.Ollama)
            .WaitFor(resourceContext.RabbitMq)
            .WaitFor(resourceContext.Elasticsearch)
            .WaitFor(resourceContext.Ollama);

        return builder;
    }
}