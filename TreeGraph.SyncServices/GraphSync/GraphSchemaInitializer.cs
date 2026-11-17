using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Persistence.Data;
using TreeGraph.Persistence.EdgeSky.Entities;
using TreeGraph.GraphSync;
using TreeGraph.GraphSync.Edge;
using TreeGraph.GraphSync.SpaceNode;
using TreeGraph.SyncServices.GraphSync.SpaceNode;
using TreeGraph.Contracts.Messaging;
using Wolverine;

namespace TreeGraph.SyncServices.GraphSync;

/// <summary>
/// NebulaGraph schema 初始化器：启动时（后台重试）按 PG spaces 逐空间确保
/// Nebula space/节点 tag/parent_of + connects_to + 该空间全部边类型存在（幂等 IF NOT EXISTS），
/// 并 DROP 掉 PG 中已删除的残留边类型（对齐删除）。
/// PG 空间 ↔ Nebula 空间 1:1（<see cref="GraphNaming.ToNebulaSpace"/> 映射）。
/// 消费者处理首条消息前调用 <see cref="EnsureReadyAsync"/> 等待就绪；
/// 全量重建后调用 <see cref="RecreateSchemaAsync"/> 重建全部空间 schema；
/// 运行时新建空间由 <see cref="EnsureSpaceSchemaAsync"/> 即时补 schema。
/// </summary>
public sealed class GraphSchemaInitializer : IHostedService, IDisposable
{
    private readonly NebulaGraphClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GraphSchemaInitializer> _logger;
    private readonly CancellationTokenSource _stopCts = new();
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GraphSchemaInitializer(
        NebulaGraphClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<GraphSchemaInitializer> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        // graphd 启动 + ADD HOSTS + CREATE SPACE 生效均有延迟，后台重试不阻塞宿主启动
        _ = Task.Run(async () =>
        {
            try
            {
                await CreateSchemaWithRetryAsync(_stopCts.Token);
                _ready.TrySetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NebulaGraph schema 初始化失败，图同步暂不可用");
                _ready.TrySetException(ex);
            }
        }, CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _stopCts.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose() => _stopCts.Dispose();

    public Task EnsureReadyAsync(CancellationToken ct = default)
        => _ready.Task.WaitAsync(ct);

    /// <summary>重建全部空间 schema（全量重建 DROP SPACE 之后调用）。</summary>
    public Task RecreateSchemaAsync(CancellationToken ct)
        => CreateSchemaWithRetryAsync(ct);

    /// <summary>为单个空间即时补 schema；失败向 consumer 抛出并进入持久化重试。</summary>
    public async Task EnsureSpaceSchemaAsync(string spaceId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
        await CreateSpaceSchemaOnceAsync(db, spaceId, ct);
        _logger.LogInformation("NebulaGraph 空间 schema 已即时补齐：{SpaceId}", spaceId);
    }

    /// <summary>删除空间的图 schema；失败向 consumer 抛出并进入持久化重试。</summary>
    public async Task DropSpaceSchemaAsync(string spaceId, CancellationToken ct = default)
    {
        await _client.QueryAsync(
            $"DROP SPACE IF EXISTS `{GraphNaming.ToNebulaSpace(spaceId)}`", ct);
        _logger.LogInformation("NebulaGraph 空间已删除：{SpaceId}", spaceId);
    }

    /// <summary>
    /// 幂等创建全部 PG 空间的图 schema（逐空间循环）。
    /// CREATE SPACE 生效需约两个心跳周期，失败由调用方重试。
    /// </summary>
    private async Task CreateSchemaOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
        var spaceIds = await db.Spaces.AsNoTracking()
            .Select(s => s.Id)
            .ToListAsync(ct);

        foreach (var spaceId in spaceIds)
            await CreateSpaceSchemaOnceAsync(db, spaceId, ct);
    }

    /// <summary>
    /// 单空间幂等创建：space（FIXED_STRING(64) vid，覆盖 GUID）、节点 tag、
    /// parent_of 树父子边（R7 起 Line→LineNode 也走 parent_of）、connects_to 端子外部连接边、
    /// 该空间 edge_types/edge_type_props 中全部边类型（含内置 line_segment，列按 PG Schema 生成）。
    /// 存量图空间的 tag/边类型缺新列时用 DESCRIBE + ALTER 补齐。
    /// </summary>
    private async Task CreateSpaceSchemaOnceAsync(
        TreeGraphDbContext db, string spaceId, CancellationToken ct)
    {
        var space = GraphNaming.ToNebulaSpace(spaceId);
        await _client.QueryAsync(
            $"CREATE SPACE IF NOT EXISTS `{space}` " +
            "(partition_num=10, replica_factor=1, vid_type=FIXED_STRING(64))", ct);

        foreach (var tag in GraphNaming.AllNodeTags)
        {
            var cols = GraphNaming.TagColumns[tag]
                .Select(name => $"`{name}` {(name == "is_archived" ? "bool" : "string")} NULL")
                .ToList();
            await _client.QueryAsync(space,
                $"CREATE TAG IF NOT EXISTS `{tag}`({string.Join(", ", cols)})", ct);
        }

        // 存量图空间：line_node / line 新增列不会因 IF NOT EXISTS 自动补，DESCRIBE 后 ALTER。
        foreach (var tag in new[] { GraphNaming.LineNodeTag, GraphNaming.LineTag })
            await EnsureTagColumnsAsync(space, tag, ct);

        await _client.QueryAsync(space,
            $"CREATE EDGE IF NOT EXISTS `{GraphNaming.ParentOfEdge}`()", ct);
        await _client.QueryAsync(space,
            $"CREATE EDGE IF NOT EXISTS `{GraphNaming.ConnectsToEdge}`(`role` string NULL, `operating_state` string NULL)", ct);

        // 语义边类型：以本空间 PG edge_types/edge_type_props 为准（内置 line_segment 经 seed 入表）
        var edgeTypes = await db.EdgeTypes.AsNoTracking()
            .Include(t => t.Props)
            .Where(t => t.SpaceId == spaceId)
            .ToListAsync(ct);

        foreach (var type in edgeTypes)
        {
            await _client.QueryAsync(space, EdgeNgqlDdlBuilder.BuildCreateEdgeNgql(type), ct);

            // 存量图空间：旧边类型缺新列不会因 IF NOT EXISTS 自动补，DESCRIBE 后 ALTER。
            await EnsureEdgeColumnsAsync(space, type, ct);
        }

        // ★ 对齐删除：PG 已物理删除的边类型在 Nebula 侧是残留
        //   （EdgeTypeService.DeleteAsync 的 DROP EDGE 失败只记日志、不回滚 PG，此处补偿）。
        await DropStaleEdgeTypesAsync(space, edgeTypes.Select(t => t.Name), ct);

        // ★ SpaceNodeSky：组合属性派生 Tag 的账本对齐（只作用受管 Tag，不碰系统/未知 Tag）
        await AlignSpaceNodeTagProjectionsAsync(db, spaceId, ct);
    }

    /// <summary>
    /// 派生 Tag 图侧对齐（启动/重建兜底，幂等）：活动行按快照 CREATE/ALTER；
    /// 墓碑行 DROP 后向核心发送条件清理命令。账本补建行与物理删除均只由核心执行。
    /// </summary>
    private async Task AlignSpaceNodeTagProjectionsAsync(
        TreeGraphDbContext db, string spaceId, CancellationToken ct)
    {
        var ownRows = await db.TagProjections.AsNoTracking()
            .Where(p => p.SpaceId == spaceId)
            .ToListAsync(ct);
        var space = GraphNaming.ToNebulaSpace(spaceId);

        foreach (var row in ownRows)
        {
            if (row.IsDeleted)
            {
                await _client.QueryAsync(space, SpaceNodeTagProjector.BuildDropTagNgql(row.TagName), ct);
                await using var scope = _scopeFactory.CreateAsyncScope();
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                await bus.PublishAsync(new TagProjectionCleanupRequested
                {
                    SpaceId = row.SpaceId,
                    SourceAttributeId = row.SourceAttributeId,
                    TagName = row.TagName,
                    Version = row.Version
                });
                _logger.LogInformation("派生 Tag 墓碑清理命令已发送：{Space}.{Tag}", space, row.TagName);
                continue;
            }

            await SpaceNodeTagSchemaAligner.EnsureTagAsync(
                _client, space, row.TagName,
                TagProjectionSchema.FromJson(row.SchemaSnapshot), _logger, ct);
        }
    }

    /// <summary>
    /// 结构派生边类型：由 GraphSynchronizer 写入、不从属于 PG edge_types，
    /// 启动对齐时必须保留，不能当作残留清理。
    /// </summary>
    private static readonly string[] StructuralEdgeTypes =
        [GraphNaming.ParentOfEdge, GraphNaming.ConnectsToEdge];

    /// <summary>
    /// 对齐删除：SHOW EDGES 列出图空间内全部边类型，DROP 掉 PG 中已不存在、且非结构派生的残留。
    /// DROP EDGE 会连带删除该类型下的全部残余边，与「PG 为唯一真源」一致。
    /// 任一 DROP 失败即抛异常，由 <see cref="CreateSchemaWithRetryAsync"/> 整体重试（CREATE 幂等）。
    /// </summary>
    private async Task DropStaleEdgeTypesAsync(
        string space, IEnumerable<string> pgEdgeTypeNames, CancellationToken ct)
    {
        var keep = pgEdgeTypeNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        keep.UnionWith(StructuralEdgeTypes);

        var shown = await _client.QueryAsync(space, "SHOW EDGES", ct);
        var stale = shown.Data?
            .Select(row => row.TryGetValue("Name", out var name) ? name.GetString() : null)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .Where(name => !keep.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        foreach (var name in stale)
            await _client.QueryAsync(space, $"DROP EDGE IF EXISTS `{name}`", ct);

        if (stale.Count > 0)
            _logger.LogInformation(
                "NebulaGraph 空间 {Space} 清理 PG 已删除的边类型：{Edges}",
                space, string.Join(",", stale));
    }

    /// <summary>DESCRIBE TAG 后补齐缺失列（新增列类型统一 string NULL）。</summary>
    private async Task EnsureTagColumnsAsync(string space, string tag, CancellationToken ct)
    {
        var described = await _client.QueryAsync(space, $"DESCRIBE TAG `{tag}`", ct);
        var existing = described.Data?
            .Select(row => row.TryGetValue("Field", out var f) ? f.GetString() : null)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var missing = GraphNaming.TagColumns[tag]
            .Where(col => !existing.Contains(col))
            .Select(col => $"`{col}` string NULL")
            .ToList();
        if (missing.Count == 0) return;

        await _client.QueryAsync(space,
            $"ALTER TAG `{tag}` ADD ({string.Join(", ", missing)})", ct);
        _logger.LogInformation("NebulaGraph tag {Space}.{Tag} 补齐列：{Columns}",
            space, tag, string.Join(",", missing));
    }

    /// <summary>DESCRIBE EDGE 后按 PG Schema 补齐缺失列（类型/NULL/DEFAULT 与列定义一致）。</summary>
    private async Task EnsureEdgeColumnsAsync(string space, EdgeType type, CancellationToken ct)
    {
        var described = await _client.QueryAsync(space, $"DESCRIBE EDGE `{type.Name}`", ct);
        var existing = described.Data?
            .Select(row => row.TryGetValue("Field", out var f) ? f.GetString() : null)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var missing = type.Props
            .Where(p => !existing.Contains(p.PropName))
            .Select(EdgeNgqlDdlBuilder.BuildColumnDef)
            .ToList();
        if (missing.Count == 0) return;

        await _client.QueryAsync(space,
            $"ALTER EDGE `{type.Name}` ADD ({string.Join(", ", missing)})", ct);
        _logger.LogInformation("NebulaGraph edge {Space}.{Edge} 补齐列：{Columns}",
            space, type.Name, string.Join(",", missing));
    }

    private async Task CreateSchemaWithRetryAsync(CancellationToken ct)
    {
        const int maxAttempts = 24; // 约 2 分钟
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await CreateSchemaOnceAsync(ct);
                if (attempt > 1)
                    _logger.LogInformation("NebulaGraph schema 初始化第 {Attempt} 次成功", attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "NebulaGraph schema 初始化第 {Attempt}/{Max} 次失败：{Msg}，5s 后重试",
                    attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}
