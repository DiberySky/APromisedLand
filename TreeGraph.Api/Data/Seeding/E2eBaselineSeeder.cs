using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEav;

namespace TreeGraph.Api.NodeEavSky.Data.Seeding;

/// <summary>
/// E2E 基线种子：item / user / project 三个实体类型的属性定义 + 示例属性值。
///
/// 目的：
///   1. 解除 NodeEav E2E 阻塞——原 item 类型无属性，EntityEdit 不渲染表单
///   2. 为后续 MAUI E2E 提供稳定、可预测的基线数据（EntityId 固定可断言）
///
/// 幂等保证：
///   - 严格检查每个实体类型/属性/示例实体是否已存在
///   - 整个 seed 包裹在事务中（含 ExecutionStrategy 重试兼容）
///   - 失败时事务回滚，不留部分数据
///
/// 与 EavSeeder 的关系：
///   - EavSeeder 种子 Product 业务示例（组合类型/自定义表/选项集）
///   - 本 Seeder 种子 E2E 基线（三个基础类型 + 固定 Id 的示例实体）
///   - 两者互不干扰，可同时启用
///
/// 依赖：UnitSeedService 已先行写入单位（本 Seeder 当前不绑定单位，
///       未来若加 net_weight 类属性，需确保单位先行）。
/// </summary>
public static class E2eBaselineSeeder
{
    // ───── 固定 EntityId 前缀，便于 E2E 断言 ─────
    public const string ItemPrefix = "item-seed-";
    public const string UserPrefix = "user-seed-";
    public const string ProjectPrefix = "project-seed-";

    // ───── 固定 InodeId 前缀 ─────
    // 一个 iNode 可声明多类型、每类型挂 1 个实体。
    // 5 个 iNode 覆盖 10 个实体：001/002 三类型齐全，003 双类型，004/005 单类型。
    public const string InodePrefix = "inode-seed-";

    public static async Task SeedAsync(TreeGraphDbContext db, CancellationToken ct = default)
    {
        if (await IsAlreadySeededAsync(db, ct))
            return;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await SeedInternalAsync(db, ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>严格幂等：item 类型 + 属性 + 示例实体 + iNode 归属都存在才跳过。</summary>
    private static async Task<bool> IsAlreadySeededAsync(TreeGraphDbContext db, CancellationToken ct)
    {
        return await db.EntityTypes.AnyAsync(t => t.EntityType == "item" && !t.IsDeleted, ct)
            && await db.AttributeCatalog.AnyAsync(
                a => a.EntityType == "item" && a.AttributeName == "name" && !a.IsDeleted, ct)
            && await db.AttributeValues.AnyAsync(
                v => v.EntityId == "item-seed-001", ct)
            && await db.InodeEntities.AnyAsync(
                x => x.InodeId == "inode-seed-001" && x.EntityId == "item-seed-001", ct);
    }

    private static async Task SeedInternalAsync(TreeGraphDbContext db, CancellationToken ct)
    {
        // ════════════════════════════════════════════════════
        // 步骤 1：实体类型
        // ════════════════════════════════════════════════════
        await EnsureEntityTypeAsync(db, "item",    "条目", "E2E 基线实体", 10, ct);
        await EnsureEntityTypeAsync(db, "user",    "用户", null,           20, ct);
        await EnsureEntityTypeAsync(db, "project", "项目", null,           30, ct);
        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 2：选项集（priority / status，挂 Shared）
        // ════════════════════════════════════════════════════
        var prioritySet = await EnsureOptionSetAsync(db, "Shared", "priority", "优先级",
            new (string Value, string Label, int Order, bool IsDefault)[]
            {
                ("low",    "低",   10, false),
                ("normal", "普通", 20, true),
                ("high",   "高",   30, false),
                ("urgent", "紧急", 40, false),
            }, ct);

        var statusSet = await EnsureOptionSetAsync(db, "Shared", "status", "状态",
            new (string Value, string Label, int Order, bool IsDefault)[]
            {
                ("draft",    "草稿",   10, true),
                ("active",   "进行中", 20, false),
                ("archived", "已归档", 30, false),
            }, ct);

        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 3：属性定义
        // ════════════════════════════════════════════════════
        // ── item ──
        var itemName     = await EnsureAttributeAsync(db, "item", "name",        "名称",     EavDataTypes.String,       true,  true,  true,  10, null,    null,                      ct);
        var itemDesc     = await EnsureAttributeAsync(db, "item", "description", "描述",     EavDataTypes.String,       false, true,  false, 20, null,    null,                      ct);
        var itemPriority = await EnsureAttributeAsync(db, "item", "priority",    "优先级",   EavDataTypes.SingleChoice, false, true,  true,  30, null,    prioritySet.OptionSetId,   ct);
        var itemDone     = await EnsureAttributeAsync(db, "item", "isDone",      "已完成",   EavDataTypes.Bool,         false, false, true,  40, "false", null,                      ct);
        var itemDue      = await EnsureAttributeAsync(db, "item", "dueDate",     "截止日期", EavDataTypes.Datetime,     false, false, true,  50, null,    null,                      ct);

        // ── user ──
        var userEmail       = await EnsureAttributeAsync(db, "user", "email",       "邮箱",   EavDataTypes.String, true,  true,  true,  10, null, null, ct);
        var userDisplayName = await EnsureAttributeAsync(db, "user", "displayName", "显示名", EavDataTypes.String, true,  true,  true,  20, null, null, ct);
        var userRole        = await EnsureAttributeAsync(db, "user", "role",        "角色",   EavDataTypes.String, false, true,  false, 30, null, null, ct);

        // ── project ──
        var projTitle  = await EnsureAttributeAsync(db, "project", "title",   "标题",   EavDataTypes.String,       true,  true, true,  10, null, null,                  ct);
        var projStatus = await EnsureAttributeAsync(db, "project", "status",  "状态",   EavDataTypes.SingleChoice, false, true, true,  20, null, statusSet.OptionSetId, ct);
        var projOwner  = await EnsureAttributeAsync(db, "project", "ownerId", "负责人", EavDataTypes.String,       false, true, false, 30, null, null,                  ct);

        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 4：示例属性值（固定 EntityId，便于 E2E 断言）
        // ════════════════════════════════════════════════════
        // ── item-seed-001 ~ 005 ──
        await UpsertStringAsync(db, "item-seed-001", "item", itemName,     "种子条目 1", ct);
        await UpsertStringAsync(db, "item-seed-001", "item", itemDesc,     "用于 E2E 断言", ct);
        await UpsertStringAsync(db, "item-seed-001", "item", itemPriority, "normal", ct);
        await UpsertBoolAsync  (db, "item-seed-001", "item", itemDone,     false, ct);

        await UpsertStringAsync(db, "item-seed-002", "item", itemName,     "种子条目 2", ct);
        await UpsertStringAsync(db, "item-seed-002", "item", itemPriority, "high", ct);
        await UpsertBoolAsync  (db, "item-seed-002", "item", itemDone,     false, ct);

        await UpsertStringAsync(db, "item-seed-003", "item", itemName,     "种子条目 3", ct);
        await UpsertStringAsync(db, "item-seed-003", "item", itemPriority, "low", ct);
        await UpsertBoolAsync  (db, "item-seed-003", "item", itemDone,     true, ct);

        await UpsertStringAsync(db, "item-seed-004", "item", itemName,     "种子条目 4", ct);
        await UpsertStringAsync(db, "item-seed-004", "item", itemPriority, "urgent", ct);
        await UpsertBoolAsync  (db, "item-seed-004", "item", itemDone,     false, ct);

        await UpsertStringAsync(db, "item-seed-005", "item", itemName,     "种子条目 5", ct);
        await UpsertStringAsync(db, "item-seed-005", "item", itemPriority, "normal", ct);
        await UpsertBoolAsync  (db, "item-seed-005", "item", itemDone,     true, ct);

        // ── user-seed-001 ~ 003 ──
        await UpsertStringAsync(db, "user-seed-001", "user", userEmail,       "alice@example.com", ct);
        await UpsertStringAsync(db, "user-seed-001", "user", userDisplayName, "Alice", ct);
        await UpsertStringAsync(db, "user-seed-001", "user", userRole,        "admin", ct);

        await UpsertStringAsync(db, "user-seed-002", "user", userEmail,       "bob@example.com", ct);
        await UpsertStringAsync(db, "user-seed-002", "user", userDisplayName, "Bob", ct);
        await UpsertStringAsync(db, "user-seed-002", "user", userRole,        "member", ct);

        await UpsertStringAsync(db, "user-seed-003", "user", userEmail,       "carol@example.com", ct);
        await UpsertStringAsync(db, "user-seed-003", "user", userDisplayName, "Carol", ct);
        await UpsertStringAsync(db, "user-seed-003", "user", userRole,        "member", ct);

        // ── project-seed-001 ~ 002 ──
        await UpsertStringAsync(db, "project-seed-001", "project", projTitle,  "种子项目 A", ct);
        await UpsertStringAsync(db, "project-seed-001", "project", projStatus, "active", ct);
        await UpsertStringAsync(db, "project-seed-001", "project", projOwner,  "user-seed-001", ct);

        await UpsertStringAsync(db, "project-seed-002", "project", projTitle,  "种子项目 B", ct);
        await UpsertStringAsync(db, "project-seed-002", "project", projStatus, "draft", ct);
        await UpsertStringAsync(db, "project-seed-002", "project", projOwner,  "user-seed-002", ct);

        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 5：iNode 归属
        // 约束：一个 iNode 可声明多类型，每类型挂 1 个实体。
        // 5 个 iNode 覆盖 10 个实体：
        //   001/002 三类型齐全（测多卡片详情页）
        //   003     双类型（item + user）
        //   004/005 单类型（仅 item）
        // 每个挂载同时写 InodeEntityType（类型声明）与 InodeEntity（实体归属）。
        // ════════════════════════════════════════════════════
        await EnsureInodeAsync(db, "inode-seed-001", "item",    "item-seed-001",    ct);
        await EnsureInodeAsync(db, "inode-seed-001", "user",    "user-seed-001",    ct);
        await EnsureInodeAsync(db, "inode-seed-001", "project", "project-seed-001", ct);

        await EnsureInodeAsync(db, "inode-seed-002", "item",    "item-seed-002",    ct);
        await EnsureInodeAsync(db, "inode-seed-002", "user",    "user-seed-002",    ct);
        await EnsureInodeAsync(db, "inode-seed-002", "project", "project-seed-002", ct);

        await EnsureInodeAsync(db, "inode-seed-003", "item", "item-seed-003", ct);
        await EnsureInodeAsync(db, "inode-seed-003", "user", "user-seed-003", ct);

        await EnsureInodeAsync(db, "inode-seed-004", "item", "item-seed-004", ct);
        await EnsureInodeAsync(db, "inode-seed-005", "item", "item-seed-005", ct);

        await db.SaveChangesAsync(ct);
    }

    // ════════════════════════════════════════════════════════
    // 私有 helper：Ensure*（不存在则创建，存在则返回已有实例）
    // ════════════════════════════════════════════════════════

    private static async Task<EntityTypeDefinition> EnsureEntityTypeAsync(
        TreeGraphDbContext db, string code, string displayName, string? description,
        int displayOrder, CancellationToken ct)
    {
        var existing = await db.EntityTypes
            .FirstOrDefaultAsync(t => t.EntityType == code && !t.IsDeleted, ct);
        if (existing != null) return existing;

        var entity = new EntityTypeDefinition
        {
            EntityType = code,
            DisplayName = displayName,
            Description = description,
            DisplayOrder = displayOrder,
        };
        db.EntityTypes.Add(entity);
        await db.SaveChangesAsync(ct);   // 回填 EntityTypeId
        return entity;
    }

    private static async Task<OptionSet> EnsureOptionSetAsync(
        TreeGraphDbContext db, string entityType, string setName, string displayName,
        (string Value, string Label, int Order, bool IsDefault)[] items,
        CancellationToken ct)
    {
        var existing = await db.OptionSets
            .Include(s => s.Items)
            .FirstOrDefaultAsync(
                s => s.EntityType == entityType && s.SetName == setName && !s.IsDeleted, ct);
        if (existing != null) return existing;

        var set = new OptionSet
        {
            EntityType = entityType,
            SetName = setName,
            DisplayName = displayName,
        };
        foreach (var (value, label, order, isDefault) in items)
        {
            set.Items.Add(new OptionItem
            {
                Value = value,
                Label = label,
                DisplayOrder = order,
                IsDefault = isDefault,
            });
        }
        db.OptionSets.Add(set);
        await db.SaveChangesAsync(ct);   // 回填 OptionSetId
        return set;
    }

    private static async Task<AttributeDefinition> EnsureAttributeAsync(
        TreeGraphDbContext db,
        string entityType, string attributeName, string displayName, string dataType,
        bool isRequired, bool isSearchable, bool isSortable, int displayOrder,
        string? defaultValue, string? refOptionSetId,
        CancellationToken ct)
    {
        var existing = await db.AttributeCatalog.FirstOrDefaultAsync(
            a => a.EntityType == entityType
              && a.AttributeName == attributeName
              && !a.IsDeleted, ct);
        if (existing != null) return existing;

        var attr = new AttributeDefinition
        {
            EntityType = entityType,
            AttributeName = attributeName,
            DisplayName = displayName,
            DataType = dataType,
            IsRequired = isRequired,
            IsSearchable = isSearchable,
            IsSortable = isSortable,
            DisplayOrder = displayOrder,
            DefaultValue = defaultValue,
            RefOptionSetId = refOptionSetId,
            Version = 1,
        };
        db.AttributeCatalog.Add(attr);
        await db.SaveChangesAsync(ct);   // 回填 AttributeId
        return attr;
    }

    // ════════════════════════════════════════════════════════
    // 私有 helper：Upsert*（按 EntityId + AttributeId 幂等）
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// 为指定 iNode 声明类型（InodeEntityType）并归属实体（InodeEntity）。
    /// 两张表均按主键幂等。
    /// </summary>
    private static async Task EnsureInodeAsync(
        TreeGraphDbContext db, string inodeId, string entityType, string entityId,
        CancellationToken ct)
    {
        var declared = await db.InodeEntityTypes.AnyAsync(
            t => t.InodeId == inodeId && t.EntityType == entityType, ct);
        if (!declared)
        {
            db.InodeEntityTypes.Add(new InodeEntityType
            {
                InodeId = inodeId,
                EntityType = entityType,
            });
        }

        var linked = await db.InodeEntities.AnyAsync(
            x => x.InodeId == inodeId && x.EntityType == entityType && x.EntityId == entityId, ct);
        if (!linked)
        {
            db.InodeEntities.Add(new InodeEntity
            {
                InodeId = inodeId,
                EntityType = entityType,
                EntityId = entityId,
            });
        }
    }

    private static async Task UpsertStringAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, string value, CancellationToken ct)
    {
        var exists = await db.AttributeValues.AnyAsync(
            v => v.EntityId == entityId
              && v.EntityType == entityType
              && v.AttributeId == attr.AttributeId, ct);
        if (exists) return;

        db.AttributeValues.Add(new AttributeValue
        {
            EntityId = entityId,
            EntityType = entityType,
            AttributeId = attr.AttributeId,
            ValueString = value,
        });
    }

    private static async Task UpsertBoolAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, bool value, CancellationToken ct)
    {
        var exists = await db.AttributeValues.AnyAsync(
            v => v.EntityId == entityId
              && v.EntityType == entityType
              && v.AttributeId == attr.AttributeId, ct);
        if (exists) return;

        db.AttributeValues.Add(new AttributeValue
        {
            EntityId = entityId,
            EntityType = entityType,
            AttributeId = attr.AttributeId,
            ValueBool = value,
        });
    }
}
