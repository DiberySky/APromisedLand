using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.HealthChecks;
using MAFWorkFlowApi.Services;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.Infrastructure;

/// <summary>
/// Program.cs 服务注册的模块化扩展。
/// 将原本集中在 Program.cs 中的数十行注册拆分为语义清晰的扩展方法。
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 LiteGraph 核心服务：配置绑定、命名 HttpClient、LiteGraphRestClient。
    /// </summary>
    public static IServiceCollection AddLiteGraph(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<LiteGraphOptions>(
            config.GetSection(LiteGraphOptions.SectionName));

        services.AddHttpClient("LiteGraph");

        services.AddSingleton<LiteGraphRestClient>();

        return services;
    }

    /// <summary>
    /// 注册 Agent 相关服务：会话存储、MAF Agent、Ollama 配置与预热。
    /// </summary>
    public static IServiceCollection AddMafAgentServices(this IServiceCollection services, IConfiguration config)
    {
        // Ollama Agent 配置绑定
        services
            .AddOptions<OllamaAgentOptions>()
            .Bind(config.GetSection(OllamaAgentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 会话存储（Redis 实现 + 接口桥接）
        services.AddSingleton<RedisAgentSessionStore>();
        services.AddSingleton<AgentSessionStore>(
            sp => sp.GetRequiredService<RedisAgentSessionStore>());
        services.AddSingleton<IConversationCatalog>(
            sp => sp.GetRequiredService<RedisAgentSessionStore>());
        services.AddSingleton<MafAgentService>();

        // Ollama 预热（fire-and-forget）
        services.AddHostedService<OllamaWarmupService>();

        return services;
    }

    /// <summary>
    /// 注册图领域服务：LiteGraph SDK、图工具、Agent 编排、语义检索。
    /// </summary>
    public static IServiceCollection AddGraphDomainServices(this IServiceCollection services, IConfiguration config)
    {
        // LiteGraph SDK + 图数据服务
        services.AddLiteGraph(config);

        services.AddSingleton<LiteGraph.Sdk.LiteGraphSdk>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LiteGraphOptions>>();
            var logger  = sp.GetRequiredService<ILogger<LiteGraphRestClient>>();
            var endpoint = LiteGraphEndpointResolver.Resolve(options.Value, logger);
            return new LiteGraph.Sdk.LiteGraphSdk(endpoint, "default");
        });

        // Graph Function Calling Agent（Scoped）
        services.AddScoped<ToolCallContext>();
        services.AddScoped<GraphTools>();
        services.AddScoped<GraphAgentService>();
        services.AddScoped<AssistantAgentService>();
        services.AddScoped<LlmAgentRouter>();

        // 图相关业务服务
        services.AddScoped<GraphExportService>();
        services.AddScoped<IntentParserService>();
        services.AddScoped<SemanticSearchService>();

        // RerankerService — Singleton（无状态，避免 Scoped 重复实例化刷屏日志）
        services.AddSingleton<RerankerService>();

        return services;
    }

    /// <summary>
    /// 注册 MCP Server 与健康检查。
    /// </summary>
    public static IServiceCollection AddMcpAndHealthChecks(this IServiceCollection services)
    {
        // MCP Server
        services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<McpGraphTools>();

        // 健康检查
        services.AddHealthChecks()
            .AddCheck<OllamaModelReadyHealthCheck>(
                name: "ollama-model-ready",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay   = TimeSpan.FromSeconds(30);
            options.Period  = TimeSpan.FromSeconds(30);
            options.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
