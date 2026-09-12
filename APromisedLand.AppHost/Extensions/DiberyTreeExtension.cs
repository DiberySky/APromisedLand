namespace APromisedLand.AppHost.Extensions;

public static class DiberyTreeExtension
{
    public static IDistributedApplicationBuilder AddDiberyTreeService(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // if (resourceContext.QuestionDb is null || resourceContext.Keycloak is null ||
        //     resourceContext.Redis is null || resourceContext.Ollama is null ||
        //     resourceContext.Nats is null || resourceContext.Elasticsearch is null ||
        //     resourceContext.TypesenseEndpoint is null) return builder;
        
        // QuestionService
        resourceContext.DiberyTreeService = builder.AddProject<Projects.DiberyTreeService>("DiberyTree-Service");
        
        if (resourceContext.TreeDb != null )
        {
            resourceContext.DiberyTreeService.WithReference(resourceContext.TreeDb);
            resourceContext.DiberyTreeService.WaitFor(resourceContext.TreeDb);
        }
        
        resourceContext.DiberyTreeService.WithOtlpExporter();

        return builder;
    }
}