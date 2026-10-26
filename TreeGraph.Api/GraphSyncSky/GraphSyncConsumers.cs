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
/// RabbitMQ 消费者：边实例变更（edges 表）→ 同步图边（幂等）。
/// Delete 依赖事件载荷（四元组）定位图边（PG 行已删）；
/// Upsert 由同步器回查 PG 读 props 整包投图。line_segment 段边同走本管道。
/// </summary>
public sealed class EdgeGraphSyncConsumer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _dedupCache;
    private readonly GraphSyncOptions _options;
    private readonly ILogger<EdgeGraphSyncConsumer> _logger;

    public EdgeGraphSyncConsumer(
        IServiceScopeFactory scopeFactory,
        IMemoryCache dedupCache,
        IOptions<GraphSyncOptions> options,
        ILogger<EdgeGraphSyncConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _dedupCache = dedupCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Consume(EdgeChangedEvent msg, CancellationToken ct)
    {
        // 去重键 = 五元组 + kind 组（upsert/delete），避免同边连续变更被窗口吞掉
        var dedupKey = $"graph-sync-edge:{msg.SpaceId}:{msg.EdgeType}:{msg.SrcVid}:{msg.Rank}:{msg.DstVid}" +
                       $":{(msg.Kind == EdgeChangeKind.Delete ? "del" : "upsert")}";
        if (_dedupCache.TryGetValue(dedupKey, out _))
        {
            _logger.LogDebug("去重窗口内跳过：{Type} {Src}->{Dst}@{Rank} {Kind}",
                msg.EdgeType, msg.SrcVid, msg.DstVid, msg.Rank, msg.Kind);
            return;
        }
        _dedupCache.Set(dedupKey, true, TimeSpan.FromSeconds(_options.DedupWindowSeconds));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<GraphSchemaInitializer>();
        var synchronizer = scope.ServiceProvider.GetRequiredService<GraphSynchronizer>();

        await initializer.EnsureReadyAsync(ct);
        await synchronizer.SyncEdgeAsync(msg, ct);
    }
}
