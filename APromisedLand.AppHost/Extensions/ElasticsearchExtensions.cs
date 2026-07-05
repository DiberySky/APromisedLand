namespace APromisedLand.AppHost.Extensions;

public static class ElasticsearchExtensions
{
    public static IDistributedApplicationBuilder AddElasticsearch(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
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

        return builder;
    }
}