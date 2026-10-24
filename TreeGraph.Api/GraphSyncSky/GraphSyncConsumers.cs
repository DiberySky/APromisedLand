using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.GraphSyncSky;

/// <summary>
/// RabbitMQ 消费者：iNode 变更 → 同步图顶点 + parent_of 边（幂等）。
/// 去重键带 kind 组（upsert/delete），避免同 Id 创建后紧跟删除被窗口吞掉。
/// </summary>
public sealed class InodeGraphSyncConsumer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _dedupCache;
    private readonly GraphSyncOptions _options;
    private readonly ILogger<InodeGraphSyncConsumer> _logger;

    public InodeGraphSyncConsumer(
        IServiceScopeFactory scopeFactory,
        IMemoryCache dedupCache,
        IOptions<GraphSyncOptions> options,
        ILogger<InodeGraphSyncConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _dedupCache = dedupCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Consume(InodeChangedEvent msg, CancellationToken ct)
    {
        var dedupKey = $"graph-sync:{msg.InodeId}:{(msg.Kind == InodeChangeKind.Deleted ? "del" : "upsert")}";
        if (_dedupCache.TryGetValue(dedupKey, out _))
        {
            _logger.LogDebug("去重窗口内跳过：{InodeId} {Kind}", msg.InodeId, msg.Kind);
            return;
        }
        _dedupCache.Set(dedupKey, true, TimeSpan.FromSeconds(_options.DedupWindowSeconds));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<GraphSchemaInitializer>();
        var synchronizer = scope.ServiceProvider.GetRequiredService<GraphSynchronizer>();

        await initializer.EnsureReadyAsync(ct);
        await synchronizer.SyncInodeAsync(msg.InodeId, ct);
    }
}

/// <summary>
/// RabbitMQ 消费者：关系属性值变更 → 同步语义边（幂等）。
/// Delete 依赖事件载荷（RelationType/From/To）定位图边（PG 行已删）。
/// R7：line_segment 段边也走本管道（段边即 relation 值）。
/// </summary>
public sealed class RelationGraphSyncConsumer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _dedupCache;
    private readonly GraphSyncOptions _options;
    private readonly ILogger<RelationGraphSyncConsumer> _logger;

    public RelationGraphSyncConsumer(
        IServiceScopeFactory scopeFactory,
        IMemoryCache dedupCache,
        IOptions<GraphSyncOptions> options,
        ILogger<RelationGraphSyncConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _dedupCache = dedupCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Consume(RelationChangedEvent msg, CancellationToken ct)
    {
        // 去重键带 kind 组（upsert/delete），避免同 ValueId 连续变更被窗口吞掉
        var dedupKey = $"graph-sync-relation:{msg.ValueId}:{(msg.Kind == RelationChangeKind.Delete ? "del" : "upsert")}";
        if (_dedupCache.TryGetValue(dedupKey, out _))
        {
            _logger.LogDebug("去重窗口内跳过：{ValueId} {Kind}", msg.ValueId, msg.Kind);
            return;
        }
        _dedupCache.Set(dedupKey, true, TimeSpan.FromSeconds(_options.DedupWindowSeconds));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<GraphSchemaInitializer>();
        var synchronizer = scope.ServiceProvider.GetRequiredService<GraphSynchronizer>();

        await initializer.EnsureReadyAsync(ct);
        await synchronizer.SyncRelationAsync(msg, ct);
    }
}
