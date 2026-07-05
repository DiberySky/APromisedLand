namespace APromisedLand.AppHost.Extensions;

public static class ElasticvueExtensions
{
    public static IDistributedApplicationBuilder AddElasticvue(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // Elasticvue
        context.Elasticvue = builder.AddContainer("Elasticvue", "cars10/elasticvue", "1.15.0")
            .WithHttpEndpoint(port: 8083, targetPort: 8080, name: "elasticvue-http")
            .WithEnvironment("ELASTICSEARCH_HOSTS", "[\"http://Elasticsearch:9200\"]")
            .WithOtlpExporter();

        return builder;
    }
}