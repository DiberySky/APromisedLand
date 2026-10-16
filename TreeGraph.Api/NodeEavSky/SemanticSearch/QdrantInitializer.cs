using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// Qdrant Collection 初始化器：启动时确保 collection 与 payload 索引存在（幂等）。
/// 消费者处理首条消息前调用 EnsureReadyAsync 等待就绪。
/// </summary>
public sealed class QdrantInitializer : IHostedService
{
    private readonly QdrantClient _client;
    private readonly SemanticSearchOptions _options;
    private readonly ILogger<QdrantInitializer> _logger;
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public QdrantInitializer(
        QdrantClient client,
        IOptions<SemanticSearchOptions> options,
        ILogger<QdrantInitializer> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync(ct);
            if (!collections.Contains(_options.CollectionName))
            {
                await _client.CreateCollectionAsync(
                    _options.CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)_options.VectorDimension,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);
                _logger.LogInformation("Qdrant collection 已创建：{Name}",
                    _options.CollectionName);
            }

            // payload 索引：host_type（space_id 隔离尚未落地，见已知限制）
            await _client.CreatePayloadIndexAsync(
                _options.CollectionName, "host_type",
                PayloadSchemaType.Keyword, cancellationToken: ct);

            _ready.TrySetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qdrant 初始化失败，语义搜索暂不可用");
            _ready.TrySetException(ex);
            // 不阻止宿主启动——搜索降级，写路径不受影响
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public Task EnsureReadyAsync(CancellationToken ct = default)
        => _ready.Task.WaitAsync(ct);
}
