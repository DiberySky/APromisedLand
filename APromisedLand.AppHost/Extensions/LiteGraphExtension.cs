using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost.Extensions;

public static class LiteGraphExtension
{
    // ─── 固定端口常量 ────────────────────────────────────────────
    private const int LiteGraphRestPort = 8701;
    private const int LiteGraphMcpPort  = 8702;
    private const int LiteGraphUiPort   = 3001;
    private const int PrometheusPort    = 9090;
    private const int GrafanaPort       = 3000;

    public static IDistributedApplicationBuilder AddLiteGraph(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // ─── 1. LiteGraph 专用数据库（复用现有 Postgres 实例）────────
        resourceContext.LiteGraphDb = resourceContext.Postgres!.AddDatabase("LiteGraphDb");

        // Ollama 端点由 Aspire 服务发现提供（容器内互访走内部网络地址），
        // 不再硬编码 http://ollama:11434，避免与端口/命名变更脱钩。
        var ollamaBaseUrl = resourceContext.Ollama!.GetEndpoint("http");

        // ─── 2. LiteGraph REST API Server ────────────────────────────
        resourceContext.LiteGraph = builder.AddLiteGraphServer("litegraph", port: LiteGraphRestPort)
            // PostgreSQL 连接配置
            .WithEnvironment("LITEGRAPH_DB_TYPE", "Postgresql")
            .WithEnvironment("LITEGRAPH_DB_HOST", "postgres")        // ← 容器服务名
            .WithEnvironment("LITEGRAPH_DB_PORT", "5432")            // ← 容器内部端口
            .WithEnvironment("LITEGRAPH_DB_NAME", "LiteGraphDb")
            .WithEnvironment("LITEGRAPH_DB_USERNAME", "postgres")
            .WithEnvironment("LITEGRAPH_DB_PASSWORD",
                resourceContext.Postgres.Resource.PasswordParameter)
            .WithEnvironment("LITEGRAPH_DB_SCHEMA", "litegraph")
            // Ollama LLM 集成配置（模型名与 Base URL 均引用统一数据源）
            .WithEnvironment("LITEGRAPH_LLM_PROVIDER", "ollama")
            .WithEnvironment("LITEGRAPH_OLLAMA_BASE_URL", ollamaBaseUrl)
            .WithEnvironment("LITEGRAPH_OLLAMA_CHAT_MODEL",      OllamaExtension.ChatModelName)
            .WithEnvironment("LITEGRAPH_OLLAMA_EMBEDDING_MODEL", OllamaExtension.EmbeddingModelName)
            .WithBindMount(
                source: Path.Combine(AppContext.BaseDirectory, "litegraph.json"),
                target: "/app/litegraph.json",
                isReadOnly: true)
            .WaitFor(resourceContext.Ollama!)   // ★ 先等 Ollama 就绪
            .WaitFor(resourceContext.LiteGraphDb!);

        // ─── 3. LiteGraph MCP Server ────────────────────────────────
        resourceContext.LiteGraphMcp = builder
            .AddContainer("litegraph-mcp", "jchristn77/litegraph-mcp", "v8.1.0")
            .WithHttpEndpoint(port: LiteGraphMcpPort, targetPort: LiteGraphMcpPort, name: "mcp-rpc")
            .WithEnvironment("LITEGRAPH_API_URL",
                resourceContext.LiteGraph.Resource.ConnectionStringExpression)
            .WaitFor(resourceContext.LiteGraph!);

        // ─── 4. LiteGraph Web UI ────────────────────────────────────
        resourceContext.LiteGraphUi = builder
            .AddContainer("litegraph-ui", "jchristn77/litegraph-ui", "v8.1.0")
            .WithHttpEndpoint(port: LiteGraphUiPort, targetPort: 3000, name: "dashboard")
            .WithEnvironment("LITEGRAPH_API_URL",
                resourceContext.LiteGraph.Resource.ConnectionStringExpression)
            .WaitFor(resourceContext.LiteGraph!)
            .WithHttpHealthCheck(path: "/", statusCode: 200, endpointName: "dashboard");

        // ─── 5. Prometheus（采集 LiteGraph 指标）────────────────────
        resourceContext.Prometheus = builder
            .AddContainer("prometheus", "prom/prometheus", "v2.53.0")
            .WithHttpEndpoint(port: PrometheusPort, targetPort: PrometheusPort, name: "prometheus-ui")
            .WithVolume("prometheus-data", "/prometheus")
            .WithVolume("prometheus-config", "/etc/prometheus")
            .WaitFor(resourceContext.LiteGraph!)
            .WithHttpHealthCheck(path: "/-/healthy", statusCode: 200, endpointName: "prometheus-ui");

        // ─── 6. Grafana OSS ─────────────────────────────────────────
        resourceContext.Grafana = builder
            .AddContainer("grafana", "grafana/grafana-oss", "11.1.0")
            .WithHttpEndpoint(port: GrafanaPort, targetPort: GrafanaPort, name: "grafana-ui")
            .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
            .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
            .WithEnvironment("PROMETHEUS_URL",
                resourceContext.Prometheus.GetEndpoint("prometheus-ui"))
            .WithVolume("grafana-data", "/var/lib/grafana")
            .WithVolume("grafana-provisioning", "/etc/grafana/provisioning")
            .WaitFor(resourceContext.Prometheus!)
            .WithHttpHealthCheck(path: "/api/health", statusCode: 200, endpointName: "grafana-ui");

        return builder;
    }

    // ─── LiteGraph 自定义资源注册 ────────────────────────────────────
    private static IResourceBuilder<LiteGraphResource> AddLiteGraphServer(
        this IDistributedApplicationBuilder builder,
        string name,
        int port = 8701)
    {
        var resource = new LiteGraphResource(name);
        return builder.AddResource(resource)
            .WithImage("jchristn77/litegraph")
            .WithImageTag("v8.1.0")
            .WithHttpEndpoint(
                port: port,
                targetPort: 8701,
                name: LiteGraphResource.HttpEndpointName);
    }
}