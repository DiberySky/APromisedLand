using Microsoft.Extensions.Hosting;

namespace APromisedLand.AppHost.Extensions;

public static class TypesenseExtension
{
    public static IDistributedApplicationBuilder AddTypesense(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // Typesense API Key
         // resourceContext.TypesenseApiKey = builder.AddParameter("typesense-api-key", secret: true);
         resourceContext.TypesenseApiKey = builder.Environment.IsDevelopment()
             ? builder.Configuration["Parameters:Typesense-api-key"] //APromisedLand.AppHost> dotnet user-secrets list
               ?? throw new InvalidOperationException("无法获取 typesense api key")
             : "${TYPESENSE_API_KEY}";
         
        // Typesense 容器
        var typesense = builder.AddContainer("Typesense", "typesense/typesense", "30.2")
            .WithArgs("--data-dir", "/data", "--api-key", resourceContext.TypesenseApiKey, "--enable-cors")
            .WithVolume("typesense-data", "/data")
            .WithEnvironment("TYPESENSE_API_KEY", resourceContext.TypesenseApiKey)
            .WithHttpEndpoint(8108, 8108, name: "typesense");

        resourceContext.Typesense = typesense;
        resourceContext.TypesenseEndpoint = typesense.GetEndpoint("typesense");
        resourceContext.TypesenseApiKey = resourceContext.TypesenseApiKey;

        return builder;
    }
}