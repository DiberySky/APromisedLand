using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Api.SpaceNodeSky.Entities;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.GraphSyncSky.SpaceNode;

/// <summary>
/// RabbitMQ 消费者：空间节点派生 Tag 的 schema 收敛（幂等）。
///
/// Version 三路比较（P0-3，逐空间账本行）：
///   msg.Version &lt; 账本行 Version → stale 丢弃，不执行任何 DDL；
///   == → 按账本快照执行 CREATE/ALTER/DROP TAG；
///   &gt; → 事件超前于账本（乱序/丢事件），按 PG 当前态执行并告警。
/// Deleted：DROP TAG IF EXISTS 成功后条件清墓碑
///   （仅当行 IsDeleted 且 Version == msg.Version，防同名重建被 stale 事件误清）。
/// DDL 只作用于账本受管 Tag，不碰系统 Tag 与未知 Tag。
/// </summary>
public sealed class SpaceNodeTagSyncConsumer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _dedupCache;
    private readonly GraphSyncOptions _options;
    private readonly ILogger<SpaceNodeTagSyncConsumer> _logger;

    public SpaceNodeTagSyncConsumer(
        IServiceScopeFactory scopeFactory,
        IMemoryCache dedupCache,
        IOptions<GraphSyncOptions> options,
        ILogger<SpaceNodeTagSyncConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _dedupCache = dedupCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Consume(SpaceNodeTagProjectionChangedEvent msg, CancellationToken ct)
    {
        // 去重键含 Version：同一来源连续变更（v1→v2）互不吞并；重投的同一消息才被窗口合并
        var dedupKey = $"space-node-tag:{msg.SourceAttributeId}:{msg.Kind}:{msg.Version}";
        if (_dedupCache.TryGetValue(dedupKey, out _))
        {
            _logger.LogDebug("去重窗口内跳过：{Source} {Kind} v{Version}",
                msg.SourceAttributeId, msg.Kind, msg.Version);
            return;
        }
        _dedupCache.Set(dedupKey, true, TimeSpan.FromSeconds(_options.DedupWindowSeconds));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<GraphSchemaInitializer>();
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<NebulaGraphClient>();

        await initializer.EnsureReadyAsync(ct);

        var query = db.TagProjections
            .Where(p => p.SourceAttributeId == msg.SourceAttributeId);
        query = msg.Kind == SpaceNodeTagProjectionChangeKind.Deleted
            ? query.Where(p => p.IsDeleted)
            : query.Where(p => !p.IsDeleted);
        if (msg.SpaceId is not null)
            query = query.Where(p => p.SpaceId == msg.SpaceId);

        var rows = await query.ToListAsync(ct);
        if (rows.Count == 0)
        {
            _logger.LogInformation(
                "Tag 投影无匹配账本行（可能已收敛）：{Source} {Kind} v{Version}",
                msg.SourceAttributeId, msg.Kind, msg.Version);
            return;
        }

        foreach (var row in rows)
        {
            if (row.Version > msg.Version)
            {
                // stale：账本已更新到更高版本，对应事件会（或已）按最新态执行
                _logger.LogDebug(
                    "stale 事件跳过：{Tag}@{Space} 账本 v{Current} > 消息 v{Msg}",
                    row.TagName, row.SpaceId, row.Version, msg.Version);
                continue;
            }
            if (msg.Version > row.Version)
                _logger.LogWarning(
                    "事件超前于账本（{Tag}@{Space} 账本 v{Current} < 消息 v{Msg}），按 PG 当前态执行",
                    row.TagName, row.SpaceId, row.Version, msg.Version);

            var space = GraphNaming.ToNebulaSpace(row.SpaceId);
            if (msg.Kind == SpaceNodeTagProjectionChangeKind.Deleted)
                await ApplyDropAsync(db, client, row, msg, space, ct);
            else
                await ApplyUpsertAsync(client, row, space, ct);
        }
    }

    /// <summary>Created/Altered：按账本快照对齐（CREATE → ADD 缺失列 / DROP 多余列）。</summary>
    private async Task ApplyUpsertAsync(
        NebulaGraphClient client, TagProjection row, string space, CancellationToken ct)
        => await SpaceNodeTagSchemaAligner.EnsureTagAsync(
            client, space, row.TagName,
            TagProjectionSchema.FromJson(row.SchemaSnapshot), _logger, ct);

    /// <summary>Deleted：DROP TAG IF EXISTS → 条件清墓碑（IsDeleted 且 Version == msg.Version）。</summary>
    private async Task ApplyDropAsync(
        TreeGraphDbContext db, NebulaGraphClient client, TagProjection row,
        SpaceNodeTagProjectionChangedEvent msg, string space, CancellationToken ct)
    {
        await client.QueryAsync(space, SpaceNodeTagProjector.BuildDropTagNgql(row.TagName), ct);
        _logger.LogInformation("派生 Tag 已删除：{Space}.{Tag}", space, row.TagName);

        if (row.Version != msg.Version)
        {
            // 版本不齐（中间事件丢失）：Tag 已删但保留墓碑行，由启动对齐/Reconciler 兜底清理
            _logger.LogWarning(
                "墓碑行版本与事件不一致（{Tag}@{Space} 行 v{Current} ≠ 消息 v{Msg}），保留账本行待对账",
                row.TagName, row.SpaceId, row.Version, msg.Version);
            return;
        }

        db.TagProjections.Remove(row);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("墓碑已清：{Tag}@{Space} v{Version}", row.TagName, row.SpaceId, msg.Version);
    }
}
