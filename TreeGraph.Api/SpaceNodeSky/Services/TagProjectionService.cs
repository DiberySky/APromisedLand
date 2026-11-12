using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.GraphSyncSky.SpaceNode;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.SpaceNodeSky.Entities;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.SpaceNodeSky.Dtos;

namespace TreeGraph.Api.SpaceNodeSky.Services;

/// <summary>投影创建结果：Conflict 非空 → 409（墓碑未清/命名冲突）；Skipped 非空 → 拒绝投影（不建行不发事件）。</summary>
public sealed record ProjectionUpsertResult(string? Conflict, string? Skipped)
{
    public static readonly ProjectionUpsertResult Ok = new(null, null);
    public static ProjectionUpsertResult ConflictResult(string error) => new(error, null);
    public static ProjectionUpsertResult SkippedResult(string reason) => new(null, reason);
}

/// <summary>
/// Tag 投影账本服务（PG-only，绝不直连 Nebula）：
/// 组合属性（DataType=composite）→ 全空间各一行的 Tag 物化账本。
/// 账本先 Version++ 持久化，再发 <see cref="SpaceNodeTagProjectionChangedEvent"/>（at-least-once）；
/// 图侧 CREATE/ALTER/DROP TAG 由消费者异步收敛（条件清墓碑防同名重建误清）。
///
/// 版本不变量：同一来源属性在全部空间的账本行共享同一 Version（每次变更全量递增）。
/// </summary>
public sealed class TagProjectionService
{
    private readonly TreeGraphDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<TagProjectionService> _logger;

    public TagProjectionService(
        TreeGraphDbContext db, IEventPublisher publisher, ILogger<TagProjectionService> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    // ============================================================
    // 属性定义生命周期（EavMetadataController 调用点）
    // ============================================================

    /// <summary>
    /// 创建属性前置校验（INSERT 前）：规范化 Tag 名命中未清墓碑 → 409。
    /// 仅 DataType=composite 时需要调用。
    /// </summary>
    public async Task<string?> CheckRecreateConflictAsync(
        string entityType, string attributeName, CancellationToken ct)
    {
        var baseName = SpaceNodeTagProjector.Normalize(
            $"{entityType}__{attributeName}", SpaceNodeTagProjector.MaxTagNameLength - 9);
        var tombstoned = await _db.TagProjections.AsNoTracking()
            .AnyAsync(p => p.TagName == baseName && p.IsDeleted, ct);
        return tombstoned
            ? $"图 Tag「{baseName}」的删除尚未在图侧生效，暂不能重建同名投影。请稍后重试。"
            : null;
    }

    /// <summary>
    /// 属性创建后：为全空间建账本行（Version=1）并发 Created 事件。
    /// 命中系统 Tag / 无引用组合类型 / 零可投影列 → 跳过（记日志，不建行不发事件）。
    /// </summary>
    public async Task<ProjectionUpsertResult> OnAttributeCreatedAsync(
        AttributeDefinition def, CancellationToken ct)
    {
        var taken = await AllTakenTagNamesAsync(def.AttributeId, ct);
        var tagName = SpaceNodeTagProjector.ComputeTagName(
            def.EntityType, def.AttributeName, def.AttributeId, taken.Contains);
        if (tagName is null)
            return ProjectionUpsertResult.ConflictResult(
                $"属性 {def.EntityType}.{def.AttributeName} 规范化后的图 Tag 名冲突（含 hash 后缀仍冲突）。");

        if (SpaceNodeTagProjector.IsSystemTag(tagName))
        {
            _logger.LogWarning(
                "派生 Tag 名命中系统 Tag，拒绝投影：{TagName}（来源 {EntityType}.{AttributeName}）",
                tagName, def.EntityType, def.AttributeName);
            return ProjectionUpsertResult.SkippedResult("system_tag");
        }

        // 同名墓碑未清 → 409（防消费者 stale 事件清掉新行）
        var tombstoned = await _db.TagProjections.AsNoTracking()
            .AnyAsync(p => p.TagName == tagName && p.IsDeleted, ct);
        if (tombstoned)
            return ProjectionUpsertResult.ConflictResult(
                $"图 Tag「{tagName}」的删除尚未在图侧生效，暂不能重建同名投影。请稍后重试。");

        var schema = await BuildSchemaAsync(def, ct);
        if (schema is null)
        {
            _logger.LogWarning(
                "组合属性未引用组合类型，拒绝投影：{EntityType}.{AttributeName}",
                def.EntityType, def.AttributeName);
            return ProjectionUpsertResult.SkippedResult("no_composite_type");
        }
        if (schema.Columns.Count == 0)
        {
            _logger.LogWarning(
                "组合类型无可投影顶层标量字段，拒绝投影：{EntityType}.{AttributeName}",
                def.EntityType, def.AttributeName);
            return ProjectionUpsertResult.SkippedResult("no_columns");
        }

        var now = DateTimeOffset.UtcNow;
        var spaceIds = await _db.Spaces.AsNoTracking().Select(s => s.Id).ToListAsync(ct);
        foreach (var spaceId in spaceIds)
        {
            _db.TagProjections.Add(new TagProjection
            {
                SpaceId = spaceId,
                TagName = tagName,
                SourceAttributeId = def.AttributeId,
                SourceEntityType = def.EntityType,
                SchemaSnapshot = schema.ToJson(),
                Version = 1,
                CreatedAt = now
            });
        }
        await _db.SaveChangesAsync(ct);

        await PublishAsync(null, def.AttributeId,
            SpaceNodeTagProjectionChangeKind.Created, 1, ct);
        return ProjectionUpsertResult.Ok;
    }

    /// <summary>
    /// 属性更新后（DataType 恒为 composite）：
    /// 有活动账本行 → 重算快照，有变化则全量 Version++ 发 Altered；引用被清空 → 走墓碑删除流。
    /// 无账本行（曾被跳过）→ 按创建流重试投影。
    /// </summary>
    public async Task OnAttributeUpdatedAsync(AttributeDefinition def, CancellationToken ct)
    {
        var rows = await _db.TagProjections
            .Where(p => p.SourceAttributeId == def.AttributeId && !p.IsDeleted)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            await OnAttributeCreatedAsync(def, ct);
            return;
        }

        if (def.RefCompositeTypeId is null)
        {
            // 组合引用被清空 → 投影失效，走墓碑删除流
            await TombstoneAndPublishAsync(def.AttributeId, rows, ct);
            return;
        }

        var schema = await BuildSchemaAsync(def, ct) ?? TagProjectionSchema.Empty;
        var snapshot = schema.ToJson();
        if (rows.All(r => r.SchemaSnapshot == snapshot)) return;

        var version = rows.Max(r => r.Version) + 1;
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows)
        {
            row.SchemaSnapshot = snapshot;
            row.Version = version;
            row.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);

        await PublishAsync(null, def.AttributeId,
            SpaceNodeTagProjectionChangeKind.Altered, version, ct);
    }

    /// <summary>属性删除后：全部账本行置墓碑（Version++）并发 Deleted 事件；消费者 DROP 成功后条件清行。</summary>
    public async Task OnAttributeDeletedAsync(AttributeDefinition def, CancellationToken ct)
    {
        var rows = await _db.TagProjections
            .Where(p => p.SourceAttributeId == def.AttributeId && !p.IsDeleted)
            .ToListAsync(ct);
        if (rows.Count == 0) return;

        await TombstoneAndPublishAsync(def.AttributeId, rows, ct);
    }

    /// <summary>
    /// 组合类型字段增删改后：重算全部引用它的组合属性的快照；
    /// 有变化 → 全量 Version++ 发 Altered（无账本行的属性按创建流重试）。
    /// </summary>
    public async Task OnCompositeFieldsChangedAsync(string compositeTypeId, CancellationToken ct)
    {
        var attrs = await _db.AttributeCatalog.AsNoTracking()
            .Where(a => a.DataType == EavDataTypes.Composite
                     && a.RefCompositeTypeId == compositeTypeId)
            .ToListAsync(ct);

        foreach (var attr in attrs)
        {
            var rows = await _db.TagProjections
                .Where(p => p.SourceAttributeId == attr.AttributeId && !p.IsDeleted)
                .ToListAsync(ct);
            if (rows.Count == 0)
            {
                // 曾被跳过（如零列），字段变化后可能已可投影
                await OnAttributeCreatedAsync(attr, ct);
                continue;
            }

            var schema = await BuildSchemaAsync(attr, ct) ?? TagProjectionSchema.Empty;
            var snapshot = schema.ToJson();
            if (rows.All(r => r.SchemaSnapshot == snapshot)) continue;

            var version = rows.Max(r => r.Version) + 1;
            var now = DateTimeOffset.UtcNow;
            foreach (var row in rows)
            {
                row.SchemaSnapshot = snapshot;
                row.Version = version;
                row.UpdatedAt = now;
            }
            await _db.SaveChangesAsync(ct);

            await PublishAsync(null, attr.AttributeId,
                SpaceNodeTagProjectionChangeKind.Altered, version, ct);
        }
    }

    // ============================================================
    // 投影计划（UI「未投影」标记；账本懒物化，状态动态计算）
    // ============================================================

    /// <summary>全部组合属性的投影计划：已投影 / 未投影（系统 Tag 冲突、零列等）/ 墓碑清理中。</summary>
    public async Task<List<TagProjectionPlanItemDto>> GetProjectionPlanAsync(CancellationToken ct)
    {
        var attrs = await _db.AttributeCatalog.AsNoTracking()
            .Where(a => a.DataType == EavDataTypes.Composite)
            .OrderBy(a => a.EntityType).ThenBy(a => a.DisplayOrder)
            .ToListAsync(ct);
        var rows = await _db.TagProjections.AsNoTracking().ToListAsync(ct);

        var plan = new List<TagProjectionPlanItemDto>(attrs.Count);
        foreach (var attr in attrs)
        {
            var own = rows.Where(r => r.SourceAttributeId == attr.AttributeId).ToList();
            if (own.Any(r => r.IsDeleted))
            {
                plan.Add(new(attr.AttributeId, attr.EntityType, attr.AttributeName,
                    own[0].TagName, "tombstoned", "图侧删除收敛中", own.Count));
                continue;
            }
            if (own.Count > 0)
            {
                plan.Add(new(attr.AttributeId, attr.EntityType, attr.AttributeName,
                    own[0].TagName, "projected", null, own.Count));
                continue;
            }

            // 无账本行 → 动态推演未投影原因
            var taken = rows.Select(r => r.TagName).ToHashSet(StringComparer.Ordinal);
            var tagName = SpaceNodeTagProjector.ComputeTagName(
                attr.EntityType, attr.AttributeName, attr.AttributeId, taken.Contains);
            var schema = await BuildSchemaAsync(attr, ct);
            var (status, reason) = tagName is null
                ? ("unprojected", "name_conflict")
                : SpaceNodeTagProjector.IsSystemTag(tagName)
                    ? ("unprojected", "system_tag")
                    : schema is null
                        ? ("unprojected", "no_composite_type")
                        : schema.Columns.Count == 0
                            ? ("unprojected", "no_columns")
                            : ("pending", null);
            plan.Add(new(attr.AttributeId, attr.EntityType, attr.AttributeName,
                tagName, status, reason, 0));
        }
        return plan;
    }

    // ============================================================
    // 内部
    // ============================================================

    /// <summary>已被其他来源占用的 Tag 名全集（含墓碑行：同名重建在创建流中单独判 409）。</summary>
    private async Task<HashSet<string>> AllTakenTagNamesAsync(string excludeSourceAttributeId, CancellationToken ct)
        => (await _db.TagProjections.AsNoTracking()
                .Where(p => p.SourceAttributeId != excludeSourceAttributeId)
                .Select(p => p.TagName)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>按属性当前引用的组合类型重算快照；未引用组合类型返回 null。</summary>
    private async Task<TagProjectionSchema?> BuildSchemaAsync(AttributeDefinition def, CancellationToken ct)
    {
        if (def.RefCompositeTypeId is null) return null;
        var fields = await _db.CompositeFields.AsNoTracking()
            .Where(f => f.CompositeTypeId == def.RefCompositeTypeId)
            .ToListAsync(ct);
        return SpaceNodeTagProjector.ProjectSchema(fields);
    }

    /// <summary>全部活动行置墓碑（Version 全量递增）并发 Deleted 事件。</summary>
    private async Task TombstoneAndPublishAsync(
        string sourceAttributeId, List<TagProjection> rows, CancellationToken ct)
    {
        var version = rows.Max(r => r.Version) + 1;
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows)
        {
            row.IsDeleted = true;
            row.DeletedAt = now;
            row.Version = version;
            row.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);

        await PublishAsync(null, sourceAttributeId,
            SpaceNodeTagProjectionChangeKind.Deleted, version, ct);
    }

    private async Task PublishAsync(
        string? spaceId, string sourceAttributeId,
        SpaceNodeTagProjectionChangeKind kind, int version, CancellationToken ct)
    {
        await _publisher.Publish(new SpaceNodeTagProjectionChangedEvent
        {
            SpaceId = spaceId,
            SourceAttributeId = sourceAttributeId,
            Kind = kind,
            Version = version
        }, ct);
        _logger.LogInformation(
            "Tag 投影事件已发布：{Kind} 来源={SourceAttributeId} 版本={Version}",
            kind, sourceAttributeId, version);
    }
}
