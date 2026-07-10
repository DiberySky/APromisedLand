namespace APromisedLand.AppHost.Extensions;

public static class ElasticKibanaExtension
{
    public static IDistributedApplicationBuilder AddElasticKibana(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.Elasticsearch is null ) return builder;

        // Kibana
        context.Kibana = builder.AddContainer("Elastic-kibana", "kibana", "8.17.3")
            .WithReference(context.Elasticsearch)
            .WithEnvironment("ELASTICSEARCH_HOSTS", "http://Elasticsearch:9200")
            .WithHttpEndpoint(port: 5601, targetPort: 5601)
            .WaitFor(context.Elasticsearch);
        
        return builder;
    }
}