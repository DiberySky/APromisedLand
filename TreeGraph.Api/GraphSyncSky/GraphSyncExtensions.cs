using MassTransit;
using Microsoft.Extensions.Options;

namespace TreeGraph.Api.GraphSyncSky;

/// <summary>图同步管道（PostgreSQL → NebulaGraph，经 TreeGraph.NebulaFastApi REST 网关）的 DI 注册。</summary>
public static class GraphSyncExtensions
{
    /// <summary>
    /// 注册图同步：选项、NebulaApi HttpClient、schema 初始化器、同步器。
    /// </summary>
    /// <param name="enableSchemaInitializer">
    /// Testing 环境（EavApiFactory 集成测试）无 NebulaGraph，
    /// 传 false 跳过 schema 初始化后台服务（消费者本就不注册）。
    /// </param>
    public static IServiceCollection AddGraphSync(
        this IServiceCollection services, IConfiguration config,
        bool enableSchemaInitializer = true)
    {
        var options = config
            .GetSection(GraphSyncOptions.SectionName)
            .Get<GraphSyncOptions>() ?? new GraphSyncOptions();

        // 配置优先级：NebulaFastApi:Endpoint（Aspire 注入）> GraphSync:NebulaApiEndpoint
        var endpoint = config["NebulaFastApi:Endpoint"] ?? options.NebulaApiEndpoint;

        services.AddOptions<GraphSyncOptions>()
            .Bind(config.GetSection(GraphSyncOptions.SectionName));

        services.AddHttpClient<NebulaGraphClient>(client =>
        {
            client.BaseAddress = new Uri(endpoint);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddSingleton<GraphSchemaInitializer>();
        if (enableSchemaInitializer)
            services.AddHostedService(sp => sp.GetRequiredService<GraphSchemaInitializer>());

        services.AddScoped<GraphSynchronizer>();

        return services;
    }

    /// <summary>
    /// 追加图同步消费者到既有 MassTransit 注册。
    /// AddMassTransit 每容器只允许调用一次，故须作为
    /// AddVectorSyncConsumers 的 configureConsumers 回调传入。
    /// </summary>
    public static void AddGraphSyncConsumers(this IBusRegistrationConfigurator x)
    {
        x.AddConsumer<InodeGraphSyncConsumer>();
        x.AddConsumer<EdgeGraphSyncConsumer>();
    }
}
