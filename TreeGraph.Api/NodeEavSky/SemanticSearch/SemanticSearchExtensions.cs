using MassTransit;
using Qdrant.Client;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>语义搜索 + 向量同步管道的 DI 注册。</summary>
public static class SemanticSearchExtensions
{
    public static IServiceCollection AddSemanticSearch(
        this IServiceCollection services, IConfiguration config)
    {
        var searchOptions = config
            .GetSection(SemanticSearchOptions.SectionName)
            .Get<SemanticSearchOptions>() ?? new SemanticSearchOptions();

        // 配置优先级：ConnectionStrings:Qdrant（Aspire 注入，格式 "Endpoint=grpc://host:6334"）
        //             > SemanticSearch:QdrantEndpoint
        var qdrantConnStr = config.GetConnectionString("Qdrant");
        var qdrantAddress = !string.IsNullOrWhiteSpace(qdrantConnStr)
            ? qdrantConnStr
            : searchOptions.QdrantEndpoint;
        // 剥离 Aspire 连接串的 "Endpoint=" 前缀，并分离 ";Key=" 段：
        // 格式 "Endpoint=grpc://host:6334;Key=<apiKey>"，若整串交给 new Uri()
        // 会把 "6334;Key=..." 当端口解析，抛 Invalid port specified。
        const string endpointPrefix = "Endpoint=";
        if (qdrantAddress.StartsWith(endpointPrefix, StringComparison.OrdinalIgnoreCase))
            qdrantAddress = qdrantAddress[endpointPrefix.Length..];

        string? qdrantApiKey = null;
        var semicolon = qdrantAddress.IndexOf(';');
        if (semicolon >= 0)
        {
            foreach (var seg in qdrantAddress[(semicolon + 1)..]
                         .Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = seg.Split('=', 2);
                if (kv.Length == 2 && kv[0].Equals("Key", StringComparison.OrdinalIgnoreCase))
                    qdrantApiKey = kv[1];
            }
            qdrantAddress = qdrantAddress[..semicolon];
        }

        // 配置优先级：Embedding:Endpoint（AppHost 注入）> SemanticSearch:EmbeddingEndpoint
        var embeddingEndpoint = config["Embedding:Endpoint"]
                                ?? searchOptions.EmbeddingEndpoint;

        services.AddOptions<SemanticSearchOptions>()
            .Bind(config.GetSection(SemanticSearchOptions.SectionName));
        services.AddOptions<VectorSyncOptions>()
            .Bind(config.GetSection(VectorSyncOptions.SectionName));

        services.AddSingleton(_ => new QdrantClient(new Uri(qdrantAddress), qdrantApiKey));
        services.AddSingleton<QdrantInitializer>();
        services.AddHostedService(sp => sp.GetRequiredService<QdrantInitializer>());

        services.AddHttpClient<EmbeddingClient>(client =>
        {
            client.BaseAddress = new Uri(embeddingEndpoint);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddSingleton<QdrantVectorStore>();
        services.AddScoped<SourceTextBuilder>();
        services.AddScoped<VectorSynchronizer>();
        services.AddScoped<SearchService>();
        services.AddHostedService<VectorReconciliationService>();

        return services;
    }

    /// <summary>RabbitMQ 消费者注册（MassTransit）。</summary>
    public static IServiceCollection AddVectorSyncConsumers(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<InodeChangedConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var rabbitUri = config.GetConnectionString("RabbitMQ")
                                ?? "amqp://localhost:5672";
                cfg.Host(new Uri(rabbitUri));
                cfg.ConfigureEndpoints(ctx);

                cfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: 5,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromMinutes(1),
                    intervalDelta: TimeSpan.FromSeconds(5)));
            });
        });

        return services;
    }
}
