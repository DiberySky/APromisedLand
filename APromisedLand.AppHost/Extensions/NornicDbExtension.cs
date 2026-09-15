namespace APromisedLand.AppHost.Extensions;

public static class NornicDbExtension
{
    public static IDistributedApplicationBuilder AddNornicDb(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // 获取 Ollama 的 HTTP 端点，Aspire 会自动解析为容器网络内地址
        var ollamaHttp = resourceContext.Ollama.GetEndpoint("http");
        
        resourceContext.NornicDb = builder.AddContainer(
                "nornicdb",
                "timothyswt/nornicdb-amd64-cpu:latest")
            .WithHttpEndpoint(port: 7474, targetPort: 7474, name: "http")
            .WithEndpoint(port: 7687, targetPort: 7687, name: "bolt")
            .WithVolume("nornicdb-data", "/data")
            .WithEnvironment("NORNICDB_LOG_LEVEL", "info")
            .WithReference(resourceContext.Ollama)

            // ===== Embeddings =====
            .WithEnvironment("NORNICDB_EMBEDDING_ENABLED", "true")
            .WithEnvironment("NORNICDB_EMBEDDING_PROVIDER", "ollama")
            .WithEnvironment("NORNICDB_EMBEDDING_API_URL", ollamaHttp)
            .WithEnvironment("NORNICDB_EMBEDDING_MODEL", "bge-large")

            // ===== Heimdall =====
            .WithEnvironment("NORNICDB_HEIMDALL_ENABLED", "true")
            .WithEnvironment("NORNICDB_HEIMDALL_PROVIDER", "ollama")
            .WithEnvironment("NORNICDB_HEIMDALL_API_URL", ollamaHttp)
            .WithEnvironment("NORNICDB_HEIMDALL_MODEL", "qwen2.5:7b")

            // ===== Security =====
            .WithEnvironment("NORNICDB_CORS_ENABLED", "false")
            .WithEnvironment("NORNICDB_NO_AUTH", "true")
            .WithEnvironment("NORNICDB_ALLOW_HTTP", "true")
            .WithEnvironment("NORNICDB_REMOTE_CREDENTIALS_KEY",
                builder.AddParameter("nornicdb-cred-key", secret: true));

        // 显式声明完整依赖链
        resourceContext.NornicDb
            .WaitFor(resourceContext.Ollama)
            .WaitFor(resourceContext.Embedding)
            .WaitFor(resourceContext.ChatModel);

        return builder;
    }
}