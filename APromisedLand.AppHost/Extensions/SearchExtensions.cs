namespace APromisedLand.AppHost.Extensions;

public static class SearchExtensions
{
    public static IDistributedApplicationBuilder AddSearch(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // Typesense API Key
        var typesenseApiKey = builder.AddParameter("typesense-api-key", secret: true);

        // Typesense 容器
        var typesense = builder.AddContainer("typesense", "typesense/typesense", "30.2")
            .WithArgs("--data-dir", "/data", "--api-key", typesenseApiKey, "--enable-cors")
            .WithVolume("typesense-data", "/data")
            .WithHttpEndpoint(8108, 8108, name: "typesense");

        context.Typesense = typesense;
        context.TypesenseEndpoint = typesense.GetEndpoint("typesense");

        if (context.RabbitMq is null) return builder;

        // Typesense-Service
        context.TypesenseService = builder.AddProject<Projects.SearchService>("Typesense-Service")
            .WithEnvironment("typesense-api-key", typesenseApiKey)
            .WithReference(context.TypesenseEndpoint)
            .WithReference(context.RabbitMq)
            .WaitFor(context.Typesense)
            .WaitFor(context.RabbitMq);

        return builder;
    }
}