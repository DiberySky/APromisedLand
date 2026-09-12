namespace APromisedLand.AppHost.Extensions;

public static class MafStatefulExtension
{
    public static IDistributedApplicationBuilder AddMafStateful(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext) 
    {
        // Add the API project with Redis and Ollama references
        var mafStateful = builder.AddProject<Projects.MafStatefulApi>("MAF-Stateful");

        if (resourceContext.Redis != null)
        {
            mafStateful.WithReference(resourceContext.Redis)
                .WaitFor(resourceContext.Redis);
        }

        if (resourceContext.ChatModel != null)
        {
            mafStateful.WithReference(resourceContext.ChatModel)
                .WaitFor(resourceContext.ChatModel);
        }

        // Add the Client project and reference the API
        builder.AddProject<Projects.MafStatefulApi_Client>("Maf-Stateful-client")
            .WithReference(mafStateful)
            .WaitFor(mafStateful);
        
        return builder;
    }
}