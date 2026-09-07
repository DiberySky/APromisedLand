namespace APromisedLand.AppHost.Extensions;

public static class MafStatefulExtension
{
    public static IDistributedApplicationBuilder AddMafStateful(
        this IDistributedApplicationBuilder builder,
        AppHostContext context) 
    {
        // Add the API project with Redis and Ollama references
        var mafStateful = builder.AddProject<Projects.MafStatefulService>("MAF-Stateful");

        if (context.Redis != null)
        {
            mafStateful.WithReference(context.Redis)
                .WaitFor(context.Redis);
        }

        if (context.AIModel != null)
        {
            mafStateful.WithReference(context.AIModel)
                .WaitFor(context.AIModel);
        }

        // Add the Client project and reference the API
        builder.AddProject<Projects.MafStatefulApi_Client>("Maf-Stateful-client")
            .WithReference(mafStateful)
            .WaitFor(mafStateful);
        
        return builder;
    }
}