using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;
using TreeGraph.Api.Data;
using TreeGraph.Api.GraphSyncSky;
using TreeGraph.Api.SpaceSky.Entities;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// 向量同步器（双向量）：统一 consumer / 对账 / 手动对账端点的同步逻辑。
///
/// 同步策略（幂等）：
///   1. 先按 inode_id 删除该节点全部旧向量（节点点 + 实体点）
///   2. 重建：节点向量（SpaceTree 节点）+ 各实体独立向量
/// 先删后写天然处理实体卸载 / 节点删除等残留场景。
/// </summary>
public sealed class VectorSynchronizer
{
    private readonly TreeGraphDbContext _db;
    private readonly SourceTextBuilder _builder;
    private readonly EmbeddingClient _embedding;
    private readonly QdrantVectorStore _store;
    private readonly ILogger<VectorSynchronizer> _logger;

    public VectorSynchronizer(
        TreeGraphDbContext db,
        SourceTextBuilder builder,
        EmbeddingClient embedding,
        QdrantVectorStore store,
        ILogger<VectorSynchronizer> logger)
    {
        _db = db;
        _builder = builder;
        _embedding = embedding;
        _store = store;
        _logger = logger;
    }

    /// <summary>同步单个 inode 的全部向量。返回是否写入（false = 已删除）。</summary>
    public async Task<bool> SyncInodeAsync(string inodeId, CancellationToken ct = default)
    {
        // 先清旧（含已卸载实体的残留向量）
        await _store.DeleteByInodeAsync(inodeId, ct);

        var source = await _builder.BuildAsync(inodeId, ct);
        if (source is null)
        {
            _logger.LogInformation("向量已删除（节点不存在且无实体）：{InodeId}", inodeId);
            return false;
        }

        // 节点向量（纯节点文本；无节点文本则跳过）
        if (!string.IsNullOrWhiteSpace(source.NodeText))
        {
            var nodeVector = await _embedding.EmbedAsync(source.NodeText, ct);
            await _store.UpsertNodeAsync(
                inodeId, nodeVector,
                source.HostType, source.SpaceId,
                source.NodeHash, source.NodeText,
                SourceTextBuilder.ViewType, SourceTextBuilder.TemplateVersion,
                source.Path, source.PathIds, source.Depth, ct);
        }

        // 各实体独立向量
        foreach (var entity in source.EntityTexts)
        {
            var entityVector = await _embedding.EmbedAsync(entity.Text, ct);
            await _store.UpsertEntityAsync(
                inodeId, entity.EntityId, entity.EntityType, entityVector,
                source.SpaceId, entity.Hash, entity.Text, ct);
        }

        _logger.LogInformation(
            "向量已同步：{InodeId} 节点={HasNode} 实体数={EntityCount} hash={Hash}",
            inodeId, !string.IsNullOrWhiteSpace(source.NodeText),
            source.EntityTexts.Count, source.NodeHash[..8]);
        return true;
    }

    /// <summary>
    /// 同步单个实体的独立向量（独立实体事件的消费入口，幂等）。
    /// 挂载关系现查：已挂载 → 文本前置宿主 path_view、payload inode_id=挂载 inode、space_id 同宿主；
    /// 未挂载 → 独立点（inode_id=""、space_id="default"，仅全局检索可见）。
    /// 无实际内容（含实体已删除）→ 删除实体点。返回是否写入。
    /// </summary>
    public async Task<bool> SyncEntityAsync(
        string entityType, string entityId, CancellationToken ct = default)
    {
        var mountInodeId = await _db.InodeEntities
            .AsNoTracking()
            .Where(m => m.EntityType == entityType && m.EntityId == entityId)
            .Select(m => m.InodeId)
            .FirstOrDefaultAsync(ct);

        string? hostNodeText = null;
        var spaceId = "default";
        if (mountInodeId is not null)
        {
            var host = await _builder.BuildAsync(mountInodeId, ct);
            hostNodeText = host?.NodeText;
            spaceId = host?.SpaceId ?? "default";
        }

        var source = await _builder.BuildEntityTextAsync(
            entityType, entityId, hostNodeText, ct);
        if (source is null)
        {
            await _store.DeletePointAsync(
                QdrantVectorStore.EntityPointUuid(entityId).ToString(), ct);
            _logger.LogInformation(
                "实体向量已删除（无内容或已删除）：{EntityType}/{EntityId}", entityType, entityId);
            return false;
        }

        var vector = await _embedding.EmbedAsync(source.Text, ct);
        await _store.UpsertEntityAsync(
            mountInodeId ?? "", entityId, entityType, vector,
            spaceId, source.Hash, source.Text, ct);

        _logger.LogInformation(
            "实体向量已同步：{EntityType}/{EntityId} 挂载={Mount} hash={Hash}",
            entityType, entityId, mountInodeId ?? "(独立)", source.Hash[..8]);
        return true;
    }

    /// <summary>全量对账（不限定空间）。</summary>
    public Task<ReconcileResult> ReconcileAllAsync(CancellationToken ct)
        => ReconcileAsync(null, null, null, onProgress: null, ct);

    /// <summary>
    /// 按空间对账：仅在该空间对应树范围内比对。
    /// 动态空间 = 空间树中以该空间为根的子树；单位空间 = 单位树全体；类型空间 = 分类树全体。
    /// </summary>
    public Task<ReconcileResult> ReconcileSpaceAsync(string spaceId, CancellationToken ct)
        => ReconcileAsync(spaceId, null, null, onProgress: null, ct);

    /// <summary>
    /// 按数据类别对账（同步刷新对话框用）：仅在该类别数据源范围内比对。
    /// space = 空间树节点；tree = 单位/分类/知识树节点；line = 线路节点。
    /// edge 在向量库无对应数据，不应传入。
    /// </summary>
    public Task<ReconcileResult> ReconcileCategoryAsync(SyncCategory category, CancellationToken ct)
        => ReconcileAsync(null, category, null, onProgress: null, ct);

    /// <summary>
    /// 按单条线路对账（线路管理页行级刷新用）：仅比对该线路自身 + 其下全部线路节点。
    /// </summary>
    public Task<ReconcileResult> ReconcileLineAsync(string lineId, CancellationToken ct)
        => ReconcileAsync(null, null, lineId, onProgress: null, ct);

    /// <summary>
    /// 对账：比较 PG 期望向量集合与 Qdrant 已存集合，补缺删孤。
    /// PG 活跃集合 = InodeEntities 挂载的 inode ∪ 4 棵宿主树节点 ∪ Lines ∪ LineNodes
    /// （Id 在 spaces 空间实体列表内的 space 节点除外，不创建向量）。
    /// 比对范围四选一：category（按数据类别）> spaceId（按空间子树）> lineId（按单线路）> 全量。
    /// onProgress：逐项异步进度回调（fix=同步节点 / purge=清理孤儿），流式端点逐行 await 推送；null 时零开销。
    /// </summary>
    public async Task<ReconcileResult> ReconcileAsync(
        string? spaceId, SyncCategory? category, string? lineId,
        Func<ReconcileProgress, Task>? onProgress, CancellationToken ct)
    {
        // 范围（null = 全量，不做范围限制）
        var scope = category is not null
            ? await ResolveCategoryScopeAsync(category.Value, ct)
            : spaceId is not null
                ? await ResolveScopeAsync(spaceId, ct)
                : lineId is not null
                    ? await ResolveLineScopeAsync(lineId, ct)
                    : null;
        if (scope is not null && scope.Count == 0)
        {
            _logger.LogInformation(
                "对账范围为空（空间={SpaceId} 类别={Category} 线路={LineId}），跳过", spaceId, category, lineId);
            return new ReconcileResult(0, 0, 0, 0, 0, 0);
        }

        // PG 侧期望：节点键 + 实体键。
        // 节点 = 4 棵宿主树（空间/单位/分类/知识）+ 线路节点；
        // space 节点（Id 存在于 spaces 空间实体列表内）不创建向量，不计入期望集合。
        var mounts = await _db.InodeEntities
            .AsNoTracking()
            .Select(m => new { m.InodeId, m.EntityId })
            .ToListAsync(ct);
        var spaceEntityIds = (await _db.Spaces
                .AsNoTracking()
                .Select(s => s.Id)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var hostNodeIds = await _db.SpaceTrees
            .AsNoTracking()
            .Where(n => !spaceEntityIds.Contains(n.Id))
            .Select(n => n.Id)
            .ToListAsync(ct);
        hostNodeIds.AddRange(await _db.UnitTrees.AsNoTracking()
            .Select(n => n.Id).ToListAsync(ct));
        hostNodeIds.AddRange(await _db.CategoryTrees.AsNoTracking()
            .Select(n => n.Id).ToListAsync(ct));
        hostNodeIds.AddRange(await _db.KnowledgeTrees.AsNoTracking()
            .Select(n => n.Id).ToListAsync(ct));
        hostNodeIds.AddRange(await _db.LineNodes.AsNoTracking()
            .Select(n => n.Id).ToListAsync(ct));
        hostNodeIds.AddRange(await _db.Lines.AsNoTracking()
            .Select(n => n.Id).ToListAsync(ct));

        if (scope is not null)
        {
            mounts = mounts.Where(m => scope.Contains(m.InodeId)).ToList();
            hostNodeIds = hostNodeIds.Where(scope.Contains).ToList();
        }

        var activeInodes = mounts.Select(m => m.InodeId)
            .Union(hostNodeIds)
            .ToHashSet(StringComparer.Ordinal);
        var entityToInode = mounts
            .GroupBy(m => m.EntityId)
            .ToDictionary(g => g.Key, g => g.First().InodeId, StringComparer.Ordinal);

        var expectedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in activeInodes)
            expectedKeys.Add(NodeVectorKey(id, SourceTextBuilder.TemplateVersion));
        foreach (var m in mounts) expectedKeys.Add("entity:" + m.EntityId);

        // 独立实体（含未挂载）：仅全量对账纳入期望集合
        // （无归属空间/线路，范围对账的 existing 过滤不含 inode_id="" 的点，纳入会误报缺失）
        Dictionary<string, string>? standaloneEntityTypes = null;
        if (scope is null)
        {
            standaloneEntityTypes = (await _db.AttributeValues
                    .AsNoTracking()
                    .Select(v => new { v.EntityId, v.EntityType })
                    .Distinct()
                    .ToListAsync(ct))
                .GroupBy(v => v.EntityId)
                .ToDictionary(g => g.Key, g => g.First().EntityType, StringComparer.Ordinal);
            foreach (var id in standaloneEntityTypes.Keys)
                expectedKeys.Add("entity:" + id);
        }

        // Qdrant 侧已存（按空间范围过滤，范围外的点本轮不处理）
        var existing = (await _store.ScrollAllPointsAsync(ct))
            .Where(p => scope is null || scope.Contains(p.InodeId))
            .ToList();
        var existingKeys = existing
            .Select(PointKey)
            .ToHashSet(StringComparer.Ordinal);

        var missing = expectedKeys.Except(existingKeys, StringComparer.Ordinal).ToList();
        var orphaned = existing
            .Where(p => !expectedKeys.Contains(PointKey(p)))
            .ToList();

        _logger.LogInformation(
            "向量对账：期望={Expected} 已存={Existing} 缺失={Missing} 孤儿={Orphaned}",
            expectedKeys.Count, existing.Count, missing.Count, orphaned.Count);

        // 补缺：按 inode 归集后整体同步（节点+实体一次完成）
        var affectedInodes = new HashSet<string>(StringComparer.Ordinal);
        var missingStandaloneEntities = new List<string>();
        foreach (var key in missing)
        {
            var sep = key.IndexOf(':');
            var kind = key[..sep];
            var rest = key[(sep + 1)..];
            if (kind == "node")
            {
                // node:{inodeId}:{viewType}:{templateVersion}
                affectedInodes.Add(rest[..rest.IndexOf(':')]);
            }
            else if (entityToInode.TryGetValue(rest, out var inodeId))
                affectedInodes.Add(inodeId);
            else if (standaloneEntityTypes is not null && standaloneEntityTypes.ContainsKey(rest))
                missingStandaloneEntities.Add(rest);
        }

        // 逐项处理范围内全部节点：缺失的补算（fix），已存在的仅校验（ok）。
        // 两者都推送进度，让对话框能展示每个节点的同步过程，而不只是缺失项。
        var names = onProgress is not null
            ? await LoadHostNamesAsync(activeInodes, ct)
            : null;
        var orderedInodes = activeInodes
            .OrderBy(id => names is not null && names.TryGetValue(id, out var n) ? n : id,
                StringComparer.Ordinal)
            .ToList();

        var fixedCount = 0;
        for (var i = 0; i < orderedInodes.Count; i++)
        {
            var inodeId = orderedInodes[i];
            var isMissing = affectedInodes.Contains(inodeId);
            if (isMissing)
            {
                await SyncInodeAsync(inodeId, ct);
                fixedCount++;
            }

            if (onProgress is not null)
            {
                // Detail = 该节点实际送去向量化的文本（SourceTextBuilder 构建结果）
                var source = await _builder.BuildAsync(inodeId, ct);
                await onProgress(new ReconcileProgress(
                    isMissing ? "fix" : "ok", inodeId,
                    names!.TryGetValue(inodeId, out var name) ? name : "",
                    source?.NodeText ?? ""));
            }
        }

        // 补缺：独立实体（无挂载 inode，不在节点循环内）
        foreach (var entityId in missingStandaloneEntities)
        {
            var entityType = standaloneEntityTypes![entityId];
            await SyncEntityAsync(entityType, entityId, ct);
            fixedCount++;
            if (onProgress is not null)
            {
                var src = await _builder.BuildEntityTextAsync(entityType, entityId, null, ct);
                await onProgress(new ReconcileProgress("fix", entityId, "", src?.Text ?? ""));
            }
        }

        // 删孤儿（按 PointId）。
        // 注意：补缺可能用同 UUID 覆盖旧点（旧单向量点 UUID=inodeId，与新节点点相同），
        // 基于对账开始时的快照删孤儿会误删刚写好的新点，因此重新快照再判定。
        var deletedCount = 0;
        if (orphaned.Count > 0)
        {
            var fresh = (await _store.ScrollAllPointsAsync(ct))
                .Where(p => scope is null || scope.Contains(p.InodeId))
                .ToList();
            var freshOrphaned = fresh
                .Where(p => !expectedKeys.Contains(PointKey(p)))
                .ToList();

            foreach (var point in freshOrphaned)
            {
                await _store.DeletePointAsync(point.Uuid, ct);
                deletedCount++;
                if (onProgress is not null)
                    await onProgress(new ReconcileProgress(
                        "purge", point.InodeId, "",
                        point.PointKind == "entity"
                            ? $"实体 {point.EntityId}"
                            : $"节点 {point.InodeId}"));
            }
        }

        if (fixedCount > 0 || deletedCount > 0)
        {
            _logger.LogInformation(
                "向量对账修复：同步 {Fixed} 个节点，删除孤儿 {Deleted} 条",
                fixedCount, deletedCount);
        }

        return new ReconcileResult(
            expectedKeys.Count, existing.Count,
            missing.Count, orphaned.Count,
            fixedCount, deletedCount);
    }

    /// <summary>节点点对账键：node:{inodeId}:{viewType}:{templateVersion}（升版即换键，旧点自动成为孤儿被清理）。</summary>
    private static string NodeVectorKey(string inodeId, string templateVersion) =>
        $"node:{inodeId}:{SourceTextBuilder.ViewType}:{templateVersion}";

    /// <summary>向量点对账键：实体点按 entityId，节点点按 inode+视图+模板版本。</summary>
    private static string PointKey(StoredVectorPoint p) =>
        p.PointKind == "entity"
            ? "entity:" + p.EntityId
            : NodeVectorKey(p.InodeId, p.TemplateVersion ?? "");

    /// <summary>
    /// BFS 收集 4 棵宿主树中 inode 的全部后代 Id（不含自身）；
    /// 若 inode 是线路，另按 LineId 收集其下全部线路节点。
    /// 改名/移动后由消费端调用，逐个级联刷新路径型向量文本。
    /// </summary>
    public async Task<List<string>> CollectDescendantIdsAsync(string inodeId, CancellationToken ct)
    {
        var result = new List<string>();
        result.AddRange(await ExpandDescendantsAsync([inodeId], _db.SpaceTrees, ct));
        result.AddRange(await ExpandDescendantsAsync([inodeId], _db.UnitTrees, ct));
        result.AddRange(await ExpandDescendantsAsync([inodeId], _db.CategoryTrees, ct));
        result.AddRange(await ExpandDescendantsAsync([inodeId], _db.KnowledgeTrees, ct));
        // 线路改名 → 其下全部线路节点的「所属线路」行失效，按 LineId 级联
        result.AddRange(await _db.LineNodes.AsNoTracking()
            .Where(n => n.LineId == inodeId)
            .Select(n => n.Id)
            .ToListAsync(ct));
        return result;
    }

    /// <summary>在单棵树表内 BFS 展开后代（不含传入根自身）。</summary>
    private static async Task<List<string>> ExpandDescendantsAsync<T>(
        IReadOnlyCollection<string> rootIds, DbSet<T> set, CancellationToken ct) where T : class
    {
        var result = new List<string>();
        var known = new HashSet<string>(rootIds, StringComparer.Ordinal);
        var frontier = rootIds.ToList();

        while (frontier.Count > 0)
        {
            var children = await set.AsNoTracking()
                .Where(n => EF.Property<string>(n, "ParentId") != null &&
                            frontier.Contains(EF.Property<string>(n, "ParentId")))
                .Select(n => EF.Property<string>(n, "Id"))
                .ToListAsync(ct);

            frontier = children.Where(id => known.Add(id)).ToList();
            result.AddRange(frontier);
        }

        return result;
    }

    /// <summary>
    /// 批量解析宿主节点名称（4 棵宿主树 + 线路节点；进度流展示用，查不到的 Id 不在结果内）。
    /// </summary>
    private async Task<Dictionary<string, string>> LoadHostNamesAsync(
        IReadOnlyCollection<string> inodeIds, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var n in await _db.SpaceTrees.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = n.Name;

        foreach (var n in await _db.UnitTrees.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = n.Name;

        foreach (var n in await _db.CategoryTrees.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = n.Name;

        foreach (var n in await _db.KnowledgeTrees.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = n.Name;

        foreach (var n in await _db.LineNodes.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = n.Name;

        foreach (var n in await _db.Lines.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = n.Name;

        return result;
    }

    /// <summary>
    /// 解析同步类别的 inode 范围（与图装载分组一致）：
    /// space = 空间树节点（排除 Id 存在于 spaces 空间实体列表内的 space 节点）；
    /// tree = 单位/分类/知识树节点；line = 线路节点；edge 无向量，返回空集合。
    /// </summary>
    private async Task<HashSet<string>> ResolveCategoryScopeAsync(
        SyncCategory category, CancellationToken ct)
    {
        switch (category)
        {
            case SyncCategory.Space:
                var spaceEntityIds = (await _db.Spaces.AsNoTracking()
                        .Select(s => s.Id).ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);
                return (await _db.SpaceTrees.AsNoTracking()
                        .Select(n => n.Id).ToListAsync(ct))
                    .Where(id => !spaceEntityIds.Contains(id))
                    .ToHashSet(StringComparer.Ordinal);

            case SyncCategory.Tree:
                var ids = await _db.UnitTrees.AsNoTracking()
                    .Select(n => n.Id).ToListAsync(ct);
                ids.AddRange(await _db.CategoryTrees.AsNoTracking()
                    .Select(n => n.Id).ToListAsync(ct));
                ids.AddRange(await _db.KnowledgeTrees.AsNoTracking()
                    .Select(n => n.Id).ToListAsync(ct));
                return ids.ToHashSet(StringComparer.Ordinal);

            case SyncCategory.Line:
                var lineScope = (await _db.Lines.AsNoTracking()
                        .Select(n => n.Id).ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);
                lineScope.UnionWith(await _db.LineNodes.AsNoTracking()
                    .Select(n => n.Id).ToListAsync(ct));
                return lineScope;

            default:
                return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 解析某空间对应的 inode 范围（与空间维护页打开树的规则一致）：
    /// 动态空间 → 空间树中以空间 Id 为根的子树（含根，根作为 space 节点后续被排除）；
    /// 单位空间 → 单位树全体节点；类型空间 → 分类树全体节点；知识库空间 → 知识树全体节点。
    /// 空间不存在时返回空集合。
    /// </summary>
    private async Task<HashSet<string>> ResolveScopeAsync(string spaceId, CancellationToken ct)
    {
        var spaceType = await _db.Spaces
            .AsNoTracking()
            .Where(s => s.Id == spaceId)
            .Select(s => (SpaceType?)s.Type)
            .FirstOrDefaultAsync(ct);

        if (spaceType is null)
            return new HashSet<string>(StringComparer.Ordinal);

        switch (spaceType.Value)
        {
            case SpaceType.Unit:
                return (await _db.UnitTrees.AsNoTracking()
                        .Select(n => n.Id).ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);

            case SpaceType.Type:
                return (await _db.CategoryTrees.AsNoTracking()
                        .Select(n => n.Id).ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);

            case SpaceType.Knowledge:
                return (await _db.KnowledgeTrees.AsNoTracking()
                        .Select(n => n.Id).ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);

            default:
                const string subtreeSql = """
                    WITH RECURSIVE subtree AS (
                        SELECT id FROM "space_trees" WHERE id = @spaceId
                        UNION ALL
                        SELECT c.id FROM "space_trees" c
                        INNER JOIN subtree s ON c."parent_id" = s.id
                    )
                    SELECT id AS "Value" FROM subtree
                    """;
                var param = new NpgsqlParameter("@spaceId", spaceId);
                var ids = await _db.Database
                    .SqlQueryRaw<string>(subtreeSql, param)
                    .ToListAsync(ct);
                return ids.ToHashSet(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 解析单条线路的 inode 范围：线路自身 + 其下全部线路节点（LineId = lineId）。
    /// 线路不存在时返回空集合。
    /// （internal：单元测试直接验证范围解析契约）
    /// </summary>
    internal async Task<HashSet<string>> ResolveLineScopeAsync(string lineId, CancellationToken ct)
    {
        var exists = await _db.Lines.AsNoTracking()
            .AnyAsync(l => l.Id == lineId, ct);
        if (!exists)
            return new HashSet<string>(StringComparer.Ordinal);

        var scope = new HashSet<string>(StringComparer.Ordinal) { lineId };
        scope.UnionWith(await _db.LineNodes.AsNoTracking()
            .Where(n => n.LineId == lineId)
            .Select(n => n.Id)
            .ToListAsync(ct));
        return scope;
    }
}

public sealed record ReconcileResult(
    int Active, int Existing, int Missing, int Orphaned,
    int FixedCount, int DeletedCount);

/// <summary>对账逐项进度事件：fix=同步节点（Name 为宿主节点名）；purge=清理孤儿（Detail 描述被删点）。</summary>
public sealed record ReconcileProgress(
    string Stage, string InodeId, string Name, string Detail);

/// <summary>
/// RabbitMQ 消费者：iNode 变更 → 重建向量（幂等）。
///
/// 幂等设计：
///   1. 消息只带 inodeId，文本从 PostgreSQL 现取（读最新已提交状态）
///   2. 滑动窗口去重：同一 inodeId 在 DedupWindowSeconds 内重复事件
///      合并为一次刷新（高频 Patch 防抖）
///   3. 同步先删后写，重复消费结果相同
///
/// 单实例部署假设：IMemoryCache 进程内去重，多实例时仅窗口失效，
/// 同步幂等保证结果仍正确。
/// </summary>
public sealed class InodeChangedConsumer
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

    public async Task Consume(InodeChangedEvent msg, CancellationToken ct)
    {
        var inodeId = msg.InodeId;

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
        var synchronizer = scope.ServiceProvider
            .GetRequiredService<VectorSynchronizer>();

        await initializer.EnsureReadyAsync(ct);
        await synchronizer.SyncInodeAsync(inodeId, ct);

        // 改名/移动（PathAffected）：自身文本已随上面刷新；全部后代的路径型文本
        // 同步失效，级联重建（后代的幂等由先删后写保证）
        if (!msg.PathAffected) return;
        var descendants = await synchronizer.CollectDescendantIdsAsync(inodeId, ct);
        _logger.LogInformation("路径级联刷新：{InodeId} 后代 {Count} 个", inodeId, descendants.Count);
        foreach (var descendantId in descendants)
            await synchronizer.SyncInodeAsync(descendantId, ct);
    }
}

/// <summary>
/// RabbitMQ 消费者：EAV 实体变更 → 重建实体独立向量（幂等）。
/// 覆盖独立实体（未挂载 inode）的写操作路径（EavController）；
/// 已挂载实体由 InodeChangedConsumer 的 SyncInodeAsync 一并重建，不经过本消费者。
/// 去重窗口与 InodeChangedConsumer 同策略（vec-sync-entity:{entityId}）。
/// </summary>
public sealed class EntityChangedConsumer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _dedupCache;
    private readonly VectorSyncOptions _syncOptions;
    private readonly ILogger<EntityChangedConsumer> _logger;

    public EntityChangedConsumer(
        IServiceScopeFactory scopeFactory,
        IMemoryCache dedupCache,
        IOptions<VectorSyncOptions> syncOptions,
        ILogger<EntityChangedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _dedupCache = dedupCache;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task Consume(EntityChangedEvent msg, CancellationToken ct)
    {
        // 滑动窗口去重：窗口内已有待处理的同实体事件则跳过
        var dedupKey = $"vec-sync-entity:{msg.EntityId}";
        if (_dedupCache.TryGetValue(dedupKey, out _))
        {
            _logger.LogDebug("去重窗口内跳过实体：{EntityId}", msg.EntityId);
            return;
        }
        _dedupCache.Set(dedupKey, true,
            TimeSpan.FromSeconds(_syncOptions.DedupWindowSeconds));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider
            .GetRequiredService<QdrantInitializer>();
        var synchronizer = scope.ServiceProvider
            .GetRequiredService<VectorSynchronizer>();

        await initializer.EnsureReadyAsync(ct);
        await synchronizer.SyncEntityAsync(msg.EntityType, msg.EntityId, ct);
    }
}
