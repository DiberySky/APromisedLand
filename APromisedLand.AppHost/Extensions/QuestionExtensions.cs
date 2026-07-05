namespace APromisedLand.AppHost.Extensions;

public static class QuestionExtensions
{
    public static IDistributedApplicationBuilder AddQuestionService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.QuestionDb is null || context.Keycloak is null ||
            context.RabbitMq is null || context.Redis is null) return builder;
        
        // QuestionService
        context.QuestionService = builder.AddProject<Projects.QuestionService>("Question-Service")
            .WithReference(context.Keycloak)
            .WithReference(context.QuestionDb)
            .WithReference(context.RabbitMq)
            .WithReference(context.Redis)
            .WaitFor(context.Keycloak)
            .WaitFor(context.QuestionDb)
            .WaitFor(context.RabbitMq)
            .WaitFor(context.Redis);

        // WeatherApi
        context.WeatherApi = builder.AddProject<Projects.WeatherApi>("Weather-Api")
            .WithReference(context.Keycloak)
            .WaitFor(context.Keycloak);

        return builder;
    }
}