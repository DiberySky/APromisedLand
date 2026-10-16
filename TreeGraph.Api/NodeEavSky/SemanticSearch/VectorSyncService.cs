using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// RabbitMQ 消费者：iNode 变更 → 重建向量（幂等）。
///
/// 幂等设计：
///   1. 消息只带 inodeId，文本从 PostgreSQL 现取（读最新已提交状态）
///   2. 滑动窗口去重：同一 inodeId 在 DedupWindowSeconds 内重复事件
///      合并为一次刷新（高频 Patch 防抖）
///   3. Qdrant upsert 天然幂等：同 PointId 重复写结果相同
///
/// 单实例部署假设：IMemoryCache 进程内去重，多实例时仅窗口失效，
/// upsert 幂等保证结果仍正确。
/// </summary>
public sealed class InodeChangedConsumer : IConsumer<InodeChangedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _dedupCache;
    private readonly VectorSyncOptions _syncOptions;
    private readonly ILogger<InodeChangedConsumer> _logger;

    public InodeChangedConsumer(
        IServiceScopeFactory scopeFactory,
        IMemoryCache dedupCache,
        IOptions<VectorSyncOptions> syncOptions,
        ILogger<InodeChangedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _dedupCache = dedupCache;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InodeChangedEvent> context)
    {
        var inodeId = context.Message.InodeId;

        // 滑动窗口去重：窗口内已有待处理的同 inodeId 事件则跳过
        var dedupKey = $"vec-sync:{inodeId}";
        if (_dedupCache.TryGetValue(dedupKey, out _))
        {
            _logger.LogDebug("去重窗口内跳过：{InodeId}", inodeId);
            return;
        }
        _dedupCache.Set(dedupKey, true,
            TimeSpan.FromSeconds(_syncOptions.DedupWindowSeconds));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider
            .GetRequiredService<QdrantInitializer>();
        var builder = scope.ServiceProvider
            .GetRequiredService<SourceTextBuilder>();
        var embedding = scope.ServiceProvider
            .GetRequiredService<EmbeddingClient>();
        var store = scope.ServiceProvider
            .GetRequiredService<QdrantVectorStore>();

        await initializer.EnsureReadyAsync(context.CancellationToken);

        // 删除事件：实体可能已全部清除，source 为 null → 删向量
        var source = await builder.BuildAsync(inodeId, context.CancellationToken);

        if (source is null)
        {
            await store.DeleteAsync(inodeId, context.CancellationToken);
            _logger.LogInformation(
                "向量已删除（宿主无实体）：{InodeId}", inodeId);
            return;
        }

        var vector = await embedding.EmbedAsync(
            source.Text, context.CancellationToken);

        await store.UpsertAsync(
            inodeId, vector,
            source.HostType, source.SpaceId, source.SourceHash, source.Text,
            context.CancellationToken);

        _logger.LogInformation(
            "向量已同步：{InodeId} kind={Kind} hash={Hash}",
            inodeId, context.Message.Kind, source.SourceHash[..8]);
    }
}
