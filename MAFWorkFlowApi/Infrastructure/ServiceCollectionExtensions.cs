using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.HealthChecks;
using MAFWorkFlowApi.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.Infrastructure;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLiteGraph(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<LiteGraphOptions>(
            config.GetSection(LiteGraphOptions.SectionName));

        services.AddHttpClient("LiteGraph");

        services.AddSingleton<LiteGraphRestClient>();

        return services;
    }

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

        // RerankerService — Singleton
        services.AddSingleton<RerankerService>();

        return services;
    }

    public static IServiceCollection AddMcpAndHealthChecks(this IServiceCollection services)
    {
        services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<McpGraphTools>();

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