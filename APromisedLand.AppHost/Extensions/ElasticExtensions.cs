namespace APromisedLand.AppHost.Extensions;

public static class ElasticExtensions
{
    public static IDistributedApplicationBuilder AddElasticsearch(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.RabbitMq is null) return builder;

        // Elasticsearch
        context.Elasticsearch = builder.AddElasticsearch("Elasticsearch")
            .WithImage("elasticsearch:9.4.3")
            .WithDockerfile("./Notes")
            .WithDataVolume("elasticsearch-data")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithHttpEndpoint(port: 9200, targetPort: 9200)
            .WithEnvironment("http.cors.enabled", "true")
            .WithEnvironment("http.cors.allow-origin", "http://localhost:8083")
            .WithEnvironment("http.cors.allow-headers", "X-Requested-With, Content-Type, Content-Length, Authorization")
            .WithOtlpExporter();

        // Elasticvue
        context.Elasticvue = builder.AddContainer("Elasticvue", "cars10/elasticvue", "1.15.0")
            .WithHttpEndpoint(port: 8083, targetPort: 8080, name: "elasticvue-http")
            .WithEnvironment("ELASTICSEARCH_HOSTS", "[\"http://Elasticsearch:9200\"]")
            .WithOtlpExporter()
            .WaitFor(context.Elasticsearch);

        // Kibana
        context.Kibana = builder.AddContainer("kibana", "kibana", "8.17.3")
            .WithReference(context.Elasticsearch)
            .WithEnvironment("ELASTICSEARCH_HOSTS", "http://Elasticsearch:9200")
            .WithHttpEndpoint(port: 5601, targetPort: 5601)
            .WaitFor(context.Elasticsearch);

        // Elastic-Service（业务服务）
        context.ElasticService = builder.AddProject<Projects.ElasticsearchService>("Elastic-Service")
            .WithReference(context.RabbitMq)
            .WithReference(context.Elasticsearch)
            .WaitFor(context.RabbitMq)
            .WaitFor(context.Elasticsearch);

        return builder;
    }
}