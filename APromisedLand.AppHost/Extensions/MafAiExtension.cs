namespace APromisedLand.AppHost.Extensions;

public static class MafAiExtension
{
    public static IDistributedApplicationBuilder AddMafAi(
        this IDistributedApplicationBuilder builder,
        AppHostContext context) 
    {
// Add the API project with Redis and Ollama references
        var mafAi = builder.AddProject<Projects.MafAIService>("MAF-AI");

        if (context.Redis != null)
        {
            mafAi.WithReference(context.Redis)
                .WaitFor(context.Redis);

        }

        if (context.AIModel != null)
        {
            mafAi.WithReference(context.AIModel)
                .WaitFor(context.AIModel);
        }

        return builder;
    }
}