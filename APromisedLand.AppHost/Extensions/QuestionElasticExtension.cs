namespace APromisedLand.AppHost.Extensions;

public static class QuestionElasticExtension
{
        public static IDistributedApplicationBuilder AddQuestionElastic(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.Elasticsearch is null || context.RabbitMq is null ||
            context.Ollama is null) return builder;
        
        // Elastic-Service（业务服务）
        context.ElasticService = builder.AddProject<Projects.ElasticsearchService>("Elastic-question")
            .WithReference(context.RabbitMq)
            .WithReference(context.Elasticsearch)
            .WithReference(context.Ollama)
            .WaitFor(context.RabbitMq)
            .WaitFor(context.Elasticsearch)
            .WaitFor(context.Ollama);

        return builder;
    }
}