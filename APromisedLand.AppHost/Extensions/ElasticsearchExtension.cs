namespace APromisedLand.AppHost.Extensions;

public static class ElasticsearchExtension
{
    public static IDistributedApplicationBuilder AddElasticsearch(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // Elasticsearch
        context.Elasticsearch = builder.AddElasticsearch("Elasticsearch")
            .WithImage("elasticsearch:9.4.3")
            .WithDockerfile("./Segmentation")
            .WithDataVolume("elasticsearch-data")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithHttpEndpoint(port: 9200, targetPort: 9200)
            .WithEnvironment("http.cors.enabled", "true")
            .WithEnvironment("http.cors.allow-origin", "http://localhost:8083")
            .WithEnvironment("http.cors.allow-headers", "X-Requested-With, Content-Type, Content-Length, Authorization")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithOtlpExporter();

        return builder;
    }
}