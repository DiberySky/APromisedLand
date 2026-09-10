namespace APromisedLand.AppHost.Extensions;

public static class MafAiExtension
{
    public static IDistributedApplicationBuilder AddMafAi(
        this IDistributedApplicationBuilder builder,
        AppHostContext context) 
    {
        // Add the API project with Redis and Ollama references
        var mafStateful = builder.AddProject<Projects.MafAIService>("MAF-Ai");

        if (context.Redis != null)
        {
            mafStateful.WithReference(context.Redis)
                .WaitFor(context.Redis);
        }

        if (context.ChatModel != null)
        {
            mafStateful.WithReference(context.ChatModel)
                .WaitFor(context.ChatModel);
        }

        // Add the Client project and reference the API
        builder.AddProject<Projects.MafStatefulApi_Client>("Maf-Stateful-client")
            .WithReference(mafStateful)
            .WaitFor(mafStateful);
        
        return builder;
    }
}