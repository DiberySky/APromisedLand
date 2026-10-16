using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// 向量对账服务（最后防线）：周期扫描两侧差集并修复。
/// 场景：RabbitMQ 重启 / 消息丢失 / Qdrant 卷损坏。
///   - PG 有、Qdrant 无 → 补算向量
///   - Qdrant 有、PG 无 → 删除孤儿向量
/// </summary>
public sealed class VectorReconciliationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VectorSyncOptions _options;
    private readonly ILogger<VectorReconciliationService> _logger;

    public VectorReconciliationService(
        IServiceScopeFactory scopeFactory,
        IOptions<VectorSyncOptions> options,
        ILogger<VectorReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.ReconciliationIntervalHours <= 0)
        {
            _logger.LogInformation("向量对账已禁用（IntervalHours=0）");
            return;
        }

        // 首次延迟 10 分钟：等系统稳定
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "向量对账失败（下次周期重试）");
            }

            await Task.Delay(
                TimeSpan.FromHours(_options.ReconciliationIntervalHours),
                stoppingToken);
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
        var builder = scope.ServiceProvider
            .GetRequiredService<SourceTextBuilder>();
        var embedding = scope.ServiceProvider
            .GetRequiredService<EmbeddingClient>();
        var store = scope.ServiceProvider
            .GetRequiredService<QdrantVectorStore>();

        // PG 侧活跃 inode 集合（有实体的节点）
        var activeIds = await db.InodeEntities
            .AsNoTracking()
            .Select(m => m.InodeId)
            .Distinct()
            .ToListAsync(ct);
        var activeSet = activeIds.ToHashSet(StringComparer.Ordinal);

        // Qdrant 侧已存向量集合
        var existingIds = await store.ScrollAllIdsAsync(ct);

        var missing = activeSet.Except(existingIds, StringComparer.Ordinal)
            .ToList();
        var orphaned = existingIds.Except(activeSet, StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation(
            "向量对账：活跃={Active} 已存={Existing} 缺失={Missing} 孤儿={Orphaned}",
            activeSet.Count, existingIds.Count, missing.Count, orphaned.Count);

        // 补算缺失（小数据集逐条，避免长时间占用嵌入模型）
        foreach (var inodeId in missing)
        {
            var source = await builder.BuildAsync(inodeId, ct);
            if (source is null) continue;

            var vector = await embedding.EmbedAsync(source.Text, ct);
            await store.UpsertAsync(inodeId, vector,
                source.HostType, source.SpaceId,
                source.SourceHash, source.Text, ct);
        }

        // 删除孤儿
        foreach (var inodeId in orphaned)
            await store.DeleteAsync(inodeId, ct);

        if (missing.Count > 0 || orphaned.Count > 0)
        {
            _logger.LogInformation(
                "向量对账修复：补算 {Missing} 条，删除孤儿 {Orphaned} 条",
                missing.Count, orphaned.Count);
        }
    }
}
