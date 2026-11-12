using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;

namespace TreeGraph.Api.GraphSyncSky.SpaceNode;

/// <summary>SpaceNode 派生 Tag 对账结果统计。</summary>
public sealed record SpaceNodeTagReconcileResult(
    int NodesChecked, int TagsAligned, int TombstonesCleared, IReadOnlyList<string> Errors);

/// <summary>
/// SpaceNode 派生 Tag 对账服务（S4.5）：
/// 1. schema 对齐 —— 全空间活动账本行逐一 EnsureTag（缺失补建/列漂移补齐）；
///    墓碑行 —— DROP TAG IF EXISTS 后物理清行（消费者清墓碑失败的兜底）；
/// 2. 值对齐 —— 全部 SpaceNode 逐一走 SpaceNodeVertexTagSyncer.SyncAsync
///    （写期望集 + 条件收敛多余受管 Tag，含「账本缺失但图侧已挂受管 Tag」的清理——
///    受管集合取全量活动账本，账本缺失的 Tag 不在期望集内会被收敛删除）。
/// 全部操作幂等，可任意重放。
/// </summary>
public sealed class SpaceNodeTagReconcileService
{
    private readonly TreeGraphDbContext _db;
    private readonly NebulaGraphClient _client;
    private readonly IServiceProvider _services;
    private readonly ILogger<SpaceNodeTagReconcileService> _logger;

    public SpaceNodeTagReconcileService(
        TreeGraphDbContext db,
        NebulaGraphClient client,
        IServiceProvider services,
        ILogger<SpaceNodeTagReconcileService> logger)
    {
        _db = db;
        _client = client;
        _services = services;
        _logger = logger;
    }

    /// <summary>执行一轮对账（逐空间：schema 对齐 → 墓碑清理 → 节点值对齐）。</summary>
    public async Task<SpaceNodeTagReconcileResult> ReconcileAsync(CancellationToken ct)
    {
        var errors = new List<string>();
        var tagsAligned = 0;
        var tombstonesCleared = 0;
        var nodesChecked = 0;

        var spaces = await _db.Spaces.AsNoTracking().Select(s => s.Id).ToListAsync(ct);
        var ledgerRows = await _db.TagProjections.ToListAsync(ct);
        var nodesBySpace = await _db.SpaceNodes.AsNoTracking()
            .Select(n => new { n.Id, n.SpaceId })
            .ToListAsync(ct);

        foreach (var spaceId in spaces)
        {
            var space = GraphNaming.ToNebulaSpace(spaceId);

            // 1) schema 对齐（活动行）
            foreach (var row in ledgerRows.Where(r => r.SpaceId == spaceId && !r.IsDeleted))
            {
                try
                {
                    await SpaceNodeTagSchemaAligner.EnsureTagAsync(
                        _client, space, row.TagName,
                        TagProjectionSchema.FromJson(row.SchemaSnapshot), _logger, ct);
                    tagsAligned++;
                }
                catch (Exception ex)
                {
                    errors.Add($"schema 对齐失败 {space}.{row.TagName}: {ex.Message}");
                }
            }

            // 2) 墓碑清理（DROP 后物理清行，幂等）
            foreach (var row in ledgerRows.Where(r => r.SpaceId == spaceId && r.IsDeleted).ToList())
            {
                try
                {
                    await _client.QueryAsync(space,
                        SpaceNodeTagProjector.BuildDropTagNgql(row.TagName), ct);
                    _db.TagProjections.Remove(row);
                    tombstonesCleared++;
                }
                catch (Exception ex)
                {
                    errors.Add($"墓碑清理失败 {space}.{row.TagName}: {ex.Message}");
                }
            }
            await _db.SaveChangesAsync(ct);

            // 3) 节点值对齐（写期望集 + 条件收敛）
            var syncer = _services.GetRequiredService<SpaceNodeVertexTagSyncer>();
            foreach (var node in nodesBySpace.Where(n => n.SpaceId == spaceId))
            {
                try
                {
                    await syncer.SyncAsync(space, spaceId, node.Id, ct);
                    nodesChecked++;
                }
                catch (Exception ex)
                {
                    errors.Add($"节点对齐失败 {space} {node.Id}: {ex.Message}");
                }
            }
        }

        _logger.LogInformation(
            "SpaceNode Tag 对账完成：节点 {Nodes} schema {Tags} 墓碑 {Tombstones} 错误 {Errors}",
            nodesChecked, tagsAligned, tombstonesCleared, errors.Count);
        return new(nodesChecked, tagsAligned, tombstonesCleared, errors);
    }
}
