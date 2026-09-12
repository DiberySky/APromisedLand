namespace APromisedLand.AppHost.Extensions;

public static class ElasticKibanaExtension
{
    public static IDistributedApplicationBuilder AddElasticKibana(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        if (resourceContext.Elasticsearch is null ) return builder;

        // Kibana
        resourceContext.Kibana = builder.AddContainer("Elastic-kibana", "kibana", "8.17.3")
            .WithReference(resourceContext.Elasticsearch)
            .WithEnvironment("ELASTICSEARCH_HOSTS", "http://Elasticsearch:9200")
            .WithHttpEndpoint(port: 5601, targetPort: 5601)
            .WaitFor(resourceContext.Elasticsearch);
        
        return builder;
    }
}