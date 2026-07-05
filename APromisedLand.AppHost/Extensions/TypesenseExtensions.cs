namespace APromisedLand.AppHost.Extensions;

public static class TypesenseExtensions
{
    public static IDistributedApplicationBuilder AddTypesense(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // Typesense API Key
        context.TypesenseApiKey = builder.AddParameter("typesense-api-key", secret: true);

        // Typesense 容器
        var typesense = builder.AddContainer("Typesense", "typesense/typesense", "30.2")
            .WithArgs("--data-dir", "/data", "--api-key", context.TypesenseApiKey, "--enable-cors")
            .WithVolume("typesense-data", "/data")
            .WithHttpEndpoint(8108, 8108, name: "typesense");

        context.Typesense = typesense;
        context.TypesenseEndpoint = typesense.GetEndpoint("typesense");
        context.TypesenseApiKey = context.TypesenseApiKey;

        return builder;
    }
}