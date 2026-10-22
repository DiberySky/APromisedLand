using TreeGraph.MafWfApi.Agents;
using TreeGraph.MafWfApi.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;

namespace TreeGraph.MafWfApi.Infrastructure;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNebulaGraph(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<NebulaGraphOptions>(
            config.GetSection(NebulaGraphOptions.SectionName));

        services.AddHttpClient("NebulaFastApi");

        services.AddSingleton<NebulaGraphRestClient>();

        return services;
    }

    public static IServiceCollection AddMafAgentServices(this IServiceCollection services, IConfiguration config)
    {
        // vLLM Agent 配置绑定
        services
            .AddOptions<AgentOptions>()
            .Bind(config.GetSection(AgentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 会话存储（Redis 实现 + 接口桥接）
        services.AddSingleton<RedisAgentSessionStore>();
        services.AddSingleton<AgentSessionStore>(
            sp => sp.GetRequiredService<RedisAgentSessionStore>());
        services.AddSingleton<IConversationCatalog>(
            sp => sp.GetRequiredService<RedisAgentSessionStore>());
        services.AddSingleton<MafAgentService>();

        // ★ 中文 → 英文变量名生成（无状态服务，走现有 IChatClient）
        services.AddSingleton<NamingService>();

        // vLLM 预热（fire-and-forget）
        services.AddHostedService<VllmWarmupService>();

        return services;
    }

    public static IServiceCollection AddGraphDomainServices(this IServiceCollection services, IConfiguration config)
    {
        // NebulaGraph 图数据客户端（经 TreeGraph.NebulaFastApi 网关）
        services.AddNebulaGraph(config);

        // Graph Function Calling Agent（Scoped）
        services.AddScoped<ToolCallContext>();
        services.AddScoped<GraphTools>();

        // ★ 语义搜索客户端（GraphAgent 的 SemanticSearchNodes 工具依赖，
        //   远程调用 TreeGraphEavApi 的 /api/search/semantic）
        services.AddHttpClient<SearchApiClient>(client =>
        {
            client.BaseAddress = new Uri(
                config["TreeGraphApi:BaseAddress"] ?? "http://localhost:5773");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ★ 子树加载客户端（复用 TreeGraphEavApi BaseAddress，走 /api/inode/.../subtree）
        services.AddHttpClient<SubtreeLoader>(client =>
        {
            client.BaseAddress = new Uri(
                config["TreeGraphApi:BaseAddress"] ?? "http://localhost:5773");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ★ 节点展开合成服务（子树 → LLM 合成回答）
        services.AddScoped<NodeExpandService>();

        services.AddScoped<GraphAgentService>();
        services.AddScoped<AssistantAgentService>();
        services.AddScoped<LlmAgentRouter>();

        return services;
    }

    public static IServiceCollection AddMcpServices(this IServiceCollection services)
    {
        services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<McpGraphTools>();

        // ★ 不注册 Ollama 健康检查：Aspire 探针走 /api/health（恒 200），
        //   vLLM 就绪探活走 /api/health/deep。
        return services;
    }
}
