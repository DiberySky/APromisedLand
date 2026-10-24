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
/// RabbitMQ 消费者：线路结构变更 → 同步图内 line_segment 结构边（幂等）。
/// PG 内线路段与通用边解耦，图内由本消费者统一拓扑。
/// </summary>
public sealed class LineStructureGraphSyncConsumer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _dedupCache;
    private readonly GraphSyncOptions _options;
    private readonly ILogger<LineStructureGraphSyncConsumer> _logger;

    public LineStructureGraphSyncConsumer(
        IServiceScopeFactory scopeFactory,
        IMemoryCache dedupCache,
        IOptions<GraphSyncOptions> options,
        ILogger<LineStructureGraphSyncConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _dedupCache = dedupCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Consume(LineStructureChangedEvent msg, CancellationToken ct)
    {
        var dedupKey = $"graph-sync-line-structure:{msg.LineId}";
        if (_dedupCache.TryGetValue(dedupKey, out _))
        {
            _logger.LogDebug("去重窗口内跳过：LineStructure {LineId}", msg.LineId);
            return;
        }
        _dedupCache.Set(dedupKey, true, TimeSpan.FromSeconds(_options.DedupWindowSeconds));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<GraphSchemaInitializer>();
        var synchronizer = scope.ServiceProvider.GetRequiredService<GraphSynchronizer>();

        await initializer.EnsureReadyAsync(ct);
        await synchronizer.SyncLineStructureAsync(msg.LineId, ct);
    }
}

/// <summary>
/// RabbitMQ 消费者：边变更 → 同步语义边（幂等）。
/// Deleted 依赖事件载荷定位图边（PG 行已删）。
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
        var dedupKey = $"graph-sync-edge:{msg.EdgeId}:{(msg.Kind == EdgeChangeKind.Deleted ? "del" : "upsert")}";
        if (_dedupCache.TryGetValue(dedupKey, out _))
        {
            _logger.LogDebug("去重窗口内跳过：{EdgeId} {Kind}", msg.EdgeId, msg.Kind);
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
