namespace APromisedLand.AppHost.Extensions;

public static class WeaviateExtension
{
    public static IDistributedApplicationBuilder AddWeaviate(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Weaviate = builder.AddContainer("weaviate", "semitechnologies/weaviate:1.26.0")
            .WithHttpEndpoint(name: "http", targetPort: 8080)
            .WithVolume("weaviate-data", "/var/lib/weaviate")

            // 认证
            .WithEnvironment("AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED", "true")

            // 存储
            .WithEnvironment("PERSISTENCE_DATA_PATH", "/var/lib/weaviate")

            // 向量化：应用侧计算向量，Weaviate 只存不算
            .WithEnvironment("DEFAULT_VECTORIZER_MODULE", "none")

            // 彻底禁用所有可选模块（含 offload-s3）
            .WithEnvironment("ENABLE_MODULES", "")

            // 关闭自动 schema
            .WithEnvironment("AUTOSCHEMA_ENABLED", "false")

            // 保留意图声明
            .WithEnvironment("OFFLOAD_S3_ENABLED", "false")

            // 启用资源限制开关
            .WithEnvironment("LIMIT_RESOURCES", "true")

            // 单节点 Raft 配置
            .WithEnvironment("CLUSTER_HOSTNAME", "node1")
            .WithEnvironment("RAFT_BOOTSTRAP_EXPECT", "1")
            .WithEnvironment("CLUSTER_IN_LOCALHOST", "true")
            .WithEnvironment("RAFT_ENABLE_ONE_NODE_RECOVERY", "true")

            // 硬性内存 / CPU 上限
            .WithContainerRuntimeArgs("--memory=4g", "--cpus=2");

        // VectorAdmin：从浏览器访问，保留固定宿主机端口 3131
        if (resourceContext.VectorAdminDb != null && resourceContext.Postgres != null)
        {
            builder.AddContainer("vectordbmanager", "mintplexlabs/vectoradmin:latest")
                .WithHttpEndpoint(port: 3131, targetPort: 3001, name: "http")
                .WithEnvironment("WEAVIATE_URL", resourceContext.Weaviate.GetEndpoint("http"))
                .WithEnvironment("DATABASE_CONNECTION_STRING", resourceContext.VectorAdminDb.Resource.UriExpression)
                .WithEnvironment("JWT_SECRET", "aVeryLongRandomStringAtLeast32CharactersLong")
                .WithEnvironment("SYS_EMAIL", "admin@vectoradmin.com")
                .WithEnvironment("SYS_PASSWORD", "Dibery#@#919")
                .WaitFor(resourceContext.Postgres)
                .WaitFor(resourceContext.Weaviate);
        }

        return builder;
    }
}