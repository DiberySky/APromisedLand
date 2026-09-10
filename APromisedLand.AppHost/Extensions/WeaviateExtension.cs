namespace APromisedLand.AppHost.Extensions;

public static class WeaviateExtension
{
    public static IDistributedApplicationBuilder AddWeaviate(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // ★ 去掉宿主机端口 8080，仅保留容器内 targetPort
        //   宿主机端口由 Aspire 动态分配，避免和本机 Tomcat/Jenkins/代理等冲突
        //   MafRagService 通过容器网络访问 weaviate:8080，不受影响
        context.Weaviate = builder.AddContainer("weaviate", "semitechnologies/weaviate:1.26.0")
            .WithHttpEndpoint(name: "http", targetPort: 8080)
            .WithVolume("weaviate-data", "/var/lib/weaviate")
            .WithEnvironment("AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED", "true")
            .WithEnvironment("PERSISTENCE_DATA_PATH", "/var/lib/weaviate")
            .WithEnvironment("DEFAULT_VECTORIZER_MODULE", "none")
            // === 单节点 Raft 配置 ===
            .WithEnvironment("CLUSTER_HOSTNAME", "node1")
            .WithEnvironment("RAFT_BOOTSTRAP_EXPECT", "1")
            .WithEnvironment("CLUSTER_IN_LOCALHOST", "true")
            .WithEnvironment("RAFT_ENABLE_ONE_NODE_RECOVERY", "true")
            .WithOtlpExporter();

        // VectorAdmin：从浏览器访问，保留固定宿主机端口 3131
        // WEAVIATE_URL 使用端点表达式，端口改动时自动跟随
        if (context.VectorAdminDb != null && context.Postgres != null)
        {
            var vectorDbManager = builder.AddContainer("vectordbmanager", "mintplexlabs/vectoradmin:latest")
                .WithHttpEndpoint(port: 3131, targetPort: 3001, name: "http")
                .WithEnvironment("WEAVIATE_URL", context.Weaviate.GetEndpoint("http"))
                .WithEnvironment("DATABASE_CONNECTION_STRING", context.VectorAdminDb.Resource.UriExpression)
                .WithEnvironment("JWT_SECRET", "aVeryLongRandomStringAtLeast32CharactersLong")
                .WithEnvironment("SYS_EMAIL", "admin@vectoradmin.com")
                .WithEnvironment("SYS_PASSWORD", "Dibery#@#919")
                .WaitFor(context.Postgres)
                .WaitFor(context.Weaviate)
                .WithOtlpExporter();
        }

        return builder;
    }
}