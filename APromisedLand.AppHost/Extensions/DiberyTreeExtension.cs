namespace APromisedLand.AppHost.Extensions;

public static class DiberyTreeExtension
{
    public static IDistributedApplicationBuilder AddDiberyTreeService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // if (context.QuestionDb is null || context.Keycloak is null ||
        //     context.Redis is null || context.Ollama is null ||
        //     context.Nats is null || context.Elasticsearch is null ||
        //     context.TypesenseEndpoint is null) return builder;
        
        // QuestionService
        context.DiberyTreeService = builder.AddProject<Projects.DiberyTreeService>("DiberyTree-Service");
        
        if (context.TreeDb != null )
        {
            context.DiberyTreeService.WithReference(context.TreeDb);
            context.DiberyTreeService.WaitFor(context.TreeDb);
        }
        
        context.DiberyTreeService.WithOtlpExporter();

        return builder;
    }
}