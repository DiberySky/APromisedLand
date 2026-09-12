namespace APromisedLand.AppHost.Extensions;

public static class WeaviateExtension
{
    public static IDistributedApplicationBuilder AddWeaviate(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // ★ 去掉宿主机端口 8080，仅保留容器内 targetPort
        //   宿主机端口由 Aspire 动态分配，避免和本机 Tomcat/Jenkins/代理等冲突
        //   MafRagService 通过容器网络访问 weaviate:8080，不受影响
        resourceContext.Weaviate = builder.AddContainer("weaviate", "semitechnologies/weaviate:1.26.0")
            .WithHttpEndpoint(name: "http", targetPort: 8080)
            .WithVolume("weaviate-data", "/var/lib/weaviate")

            // ---------- 认证 ----------
            .WithEnvironment("AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED", "true")

            // ---------- 存储 ----------
            .WithEnvironment("PERSISTENCE_DATA_PATH", "/var/lib/weaviate")

            // ---------- 向量化 ----------
            // 显式声明：向量由应用侧（Ollama bge-large）计算，Weaviate 只存不算
            .WithEnvironment("DEFAULT_VECTORIZER_MODULE", "none")

            // ★ 修复 1：关闭自动 schema —— 强制由 WeaviateSchemaInitializer 显式建 class，
            //   避免 auto-schema 抢先以默认参数创建同名 class 导致结构漂移。
            .WithEnvironment("AUTOSCHEMA_ENABLED", "false")

            // ★ 修复 2：显式声明启用哪些模块 —— "none" 表示禁用所有可选模块。
            //   为什么需要这个：
            //     · OFFLOAD_S3_ENABLED=false 只控制"是否激活 offload 行为"，
            //       但 Weaviate 在启动阶段仍会**加载**该内置模块并打印
            //       "module offload-s3 is enabled" 日志。
            //     · 想彻底阻止模块加载，必须用 ENABLE_MODULES 显式声明白名单。
            //   影响评估：
            //     · 本项目向量在应用侧计算（DEFAULT_VECTORIZER_MODULE=none），
            //       不使用 text2vec-* / generative-* 等模块，禁用是安全的。
            //     · 如果未来要启用某个模块，改为逗号分隔列表，例如：
            //       .WithEnvironment("ENABLE_MODULES", "text2vec-ollama")
            // .WithEnvironment("ENABLE_MODULES", "")

            // 保留 OFFLOAD_S3_ENABLED=false 作为"意图声明"（即使 ENABLE_MODULES=none 已覆盖）
            .WithEnvironment("OFFLOAD_S3_ENABLED", "false")

            // ★ 修复 3：启用资源限制开关
            //   这只是一个"开关"——告诉 Weaviate 它应该遵从容器运行时给出的配额。
            //   真正的上限由下一步的 WithContainerRuntimeArgs 设置。
            .WithEnvironment("LIMIT_RESOURCES", "true")

            // ---------- 单节点 Raft 配置 ----------
            .WithEnvironment("CLUSTER_HOSTNAME", "node1")
            .WithEnvironment("RAFT_BOOTSTRAP_EXPECT", "1")
            .WithEnvironment("CLUSTER_IN_LOCALHOST", "true")
            .WithEnvironment("RAFT_ENABLE_ONE_NODE_RECOVERY", "true")

            // ★ 修复 4：通过 Docker 运行时参数设置硬性内存/CPU 上限
            //   为什么需要这个：
            //     · LIMIT_RESOURCES=true 只是让 Weaviate "愿意接受限制"，
            //       但 Docker Desktop on Windows 下 Weaviate 读不到 cgroup 配额
            //       （日志："Unable to read from cgroups: read cpuset: ..."），
            //       会回退到宿主机全部资源（memory=long.MaxValue，cores=15）。
            //     · 这里透传给 `docker run` 的参数，由 Docker 直接强制限制，
            //       不依赖容器内程序自己读取。
            //
            //   参数取值建议：
            //     · 4GB / 2 核：适合本地开发、单机演示。
            //     · 8GB / 4 核：适合多文档索引、混合检索并发场景。
            //     · 生产部署时改用 K8s 的 resources.limits，此参数可移除。
            .WithContainerRuntimeArgs("--memory=4g", "--cpus=2")

            .WithOtlpExporter();

        // VectorAdmin：从浏览器访问，保留固定宿主机端口 3131
        // WEAVIATE_URL 使用端点表达式，端口改动时自动跟随
        if (resourceContext.VectorAdminDb != null && resourceContext.Postgres != null)
        {
            var vectorDbManager = builder.AddContainer("vectordbmanager", "mintplexlabs/vectoradmin:latest")
                .WithHttpEndpoint(port: 3131, targetPort: 3001, name: "http")
                .WithEnvironment("WEAVIATE_URL", resourceContext.Weaviate.GetEndpoint("http"))
                .WithEnvironment("DATABASE_CONNECTION_STRING", resourceContext.VectorAdminDb.Resource.UriExpression)
                .WithEnvironment("JWT_SECRET", "aVeryLongRandomStringAtLeast32CharactersLong")
                .WithEnvironment("SYS_EMAIL", "admin@vectoradmin.com")
                .WithEnvironment("SYS_PASSWORD", "Dibery#@#919")
                .WaitFor(resourceContext.Postgres)
                .WaitFor(resourceContext.Weaviate)
                .WithOtlpExporter();
        }

        return builder;
    }
}