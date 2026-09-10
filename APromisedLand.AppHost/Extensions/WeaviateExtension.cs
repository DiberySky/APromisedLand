namespace APromisedLand.AppHost.Extensions;

public static class WeaviateExtension
{
    public static IDistributedApplicationBuilder AddWeaviate(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        context.Weaviate = builder.AddContainer("weaviate", "semitechnologies/weaviate:1.26.0")
            .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
            .WithVolume("weaviate-data", "/var/lib/weaviate")
            .WithEnvironment("AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED", "true")
            .WithEnvironment("PERSISTENCE_DATA_PATH", "/var/lib/weaviate")
            .WithEnvironment("DEFAULT_VECTORIZER_MODULE", "none")
            // === 新增：单节点 Raft 配置 ===
            .WithEnvironment("CLUSTER_HOSTNAME", "node1")           // 固定节点主机名，避免升级/重启时节点名变化导致 Raft 配置不一致
            .WithEnvironment("RAFT_BOOTSTRAP_EXPECT", "1")           // 期望引导的节点数为 1（单节点模式）
            .WithEnvironment("CLUSTER_IN_LOCALHOST", "true")         // 集群通信使用 localhost
            .WithEnvironment("RAFT_ENABLE_ONE_NODE_RECOVERY", "true") // 单节点恢复模式
            .WithOtlpExporter();
        
        // 使用官方镜像 mintplexlabs/vectoradmin
        if (context.VectorAdminDb != null && context.Postgres != null) 
        {
            var vectorDbManager = builder.AddContainer("vectordbmanager", "mintplexlabs/vectoradmin:latest")
                .WithHttpEndpoint(port: 3131, targetPort: 3001, name: "http")
                .WithEnvironment("WEAVIATE_URL", context.Weaviate.GetEndpoint("http"))
                .WithEnvironment("DATABASE_CONNECTION_STRING", context.VectorAdminDb.Resource.UriExpression)
                .WithEnvironment("JWT_SECRET", "aVeryLongRandomStringAtLeast32CharactersLong") // 新增
                .WithEnvironment("SYS_EMAIL", "admin@vectoradmin.com")       // 可选，用于首次登录
                .WithEnvironment("SYS_PASSWORD", "Dibery#@#919")             // 可选，用于首次登录
                .WaitFor(context.Postgres)
                .WaitFor(context.Weaviate)
                .WithOtlpExporter();
        }
        
        return builder;
    }
}