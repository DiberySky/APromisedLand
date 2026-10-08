using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.StringTreeSky.Entities;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>
/// StringTreeSky + EAV 集成演示种子：
///   1 个 Space（独立 EntityType）→ 5 个 StringTree 节点（≥3）→ 全套 EAV 属性（覆盖 14 种 DataType，含 file）。
///
/// 目的：端到端验证"Space → StringTree → EAV"链路，前端可在 SpaceListPage / StringTreeSky
/// 直接看到示例数据并通过 NodePropertySummaryService / DynamicForm 查询/编辑属性。
///
/// ★ 多节点共享 EntityType + 独立属性实例：
///   4 个子节点（demo-node-001/002/003/004）共享同一个 EntityType = "StringTreeNode:demo-space-001"，
///   即共用同一套 15 个 AttributeDefinition，但每个节点有独立的 13 条 AttributeValue 行
///   （以 EntityId 区分）。同属性在不同节点上值各异：
///     title       → "旗舰手机" / "超薄笔记本" / "实木办公桌" / "iPhone 15 Pro"
///     priority    → "high" / "normal" / "low" / "urgent"
///     status      → "active" / "active" / "draft" / "active"
///     attachment  → 4 份不同文件元数据（DocId 各异）
///   测试 StringTreeDemoSeederTests.MultiNodes_ShareEntityType_ButHaveIndependentAttributeValues 验证之。
///
/// 数据布局（固定 Id 便于 E2E 断言）：
///   EntityType = "StringTreeNode:demo-space-001"（SpaceController 约定的独立类型格式）
///   ┌─ demo-space-001  演示空间（根 / Space 本体）
///   │   ├─ demo-node-001  手机
///   │   │   └─ demo-node-004  iPhone 15 Pro（孙节点）
///   │   ├─ demo-node-002  笔记本电脑
///   │   └─ demo-node-003  办公桌
///
/// 属性覆盖（14 种 DataType）：
///   string / int / decimal / bool / datetime / date / time / file / json /
///   single_choice / composite / table
///
/// ★ file 属性说明：
///   ValueFileMeta (JsonDocument) 仅存元数据（与 FileMetadataDto 对齐：DocId/FileName/Size/Sha256 等），
///   二进制实体本身存于独立 TreeGraph.FileStorageApi（不在 demo 测试范围）。
///   Seeder 直接构造元数据 JSON 写入，模拟"文件已上传完成"状态——
///   真正的上下传集成测试应在 FileStorageApiTests 项目中（需启动 SeaweedFS 容器）。
///
/// 幂等保证：
///   - 严格检查 Space 节点 + EntityType + 全部属性是否已存在
///   - 整个 seed 包裹在事务中（含 ExecutionStrategy 重试兼容）
///   - 失败时事务回滚，不留部分数据
///
/// 依赖：
///   - UnitSeedService 已先写入 kg 单位（weight 属性绑定）
///   - 应在 EavSeeder / StringTreeNodeSeeder 之后执行（互不冲突）
/// </summary>
public static class StringTreeDemoSeeder
{
    // ───── 固定 Id / EntityType ─────
    public const string SpaceEntityType = "StringTreeNode:demo-space-001";
    public const string SpaceNodeId    = "demo-space-001";
    public const string NodeIdPrefix    = "demo-node-";

    // ★ 第二个 Space：用于验证"两个 Space 的 EntityType 独占、属性定义不共享"
    //   - 与 demo-space-001 同样遵循 SpaceController 的命名约定 StringTreeNode:{spaceId}
    //   - 同名属性（title/quantity/priority/status/weight）在两个 EntityType 下是不同 AttributeDefinition
    //   - 选项集 priority 在两个 EntityType 下也是不同 OptionSet
    public const string SecondSpaceEntityType = "StringTreeNode:demo-space-002";
    public const string SecondSpaceNodeId     = "demo-space-002";

    // kg 单位固定 GUID（与 UnitSeedService 一致）
    private static readonly Guid KgUnitId =
        Guid.Parse("e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e");

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

    /// <summary>严格幂等：两个 Space 节点 + 各自 EntityType + title 属性都存在才跳过。</summary>
    private static async Task<bool> IsAlreadySeededAsync(
        TreeGraphDbContext db, CancellationToken ct)
    {
        return await db.StringTreeSkyNodes.AnyAsync(n => n.Id == SpaceNodeId, ct)
            && await db.StringTreeSkyNodes.AnyAsync(n => n.Id == SecondSpaceNodeId, ct)
            && await db.EntityTypes.AnyAsync(
                t => t.EntityType == SpaceEntityType && !t.IsDeleted, ct)
            && await db.EntityTypes.AnyAsync(
                t => t.EntityType == SecondSpaceEntityType && !t.IsDeleted, ct)
            && await db.AttributeCatalog.AnyAsync(
                a => a.EntityType == SpaceEntityType && a.AttributeName == "title", ct)
            && await db.AttributeCatalog.AnyAsync(
                a => a.EntityType == SecondSpaceEntityType && a.AttributeName == "title", ct);
    }

    private static async Task SeedInternalAsync(TreeGraphDbContext db, CancellationToken ct)
    {
        // ════════════════════════════════════════════════════
        // 步骤 1：注册 EntityType（独立类型）
        // ════════════════════════════════════════════════════
        await EnsureEntityTypeAsync(db, SpaceEntityType, "演示空间",
            "StringTreeSky + EAV 集成演示种子空间", 100, ct);
        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 2：StringTree 节点（Space 根 + 4 子节点，超出最少 3 个要求）
        // ════════════════════════════════════════════════════
        var space = new StringNodeEntity
        {
            Id          = SpaceNodeId,
            Name        = "演示空间",
            ParentId    = null,
            Description = "端到端演示用 Space",
            SortOrder   = 100,
            EntityType  = SpaceEntityType
        };
        var phone       = NewNode(SpaceNodeId, "手机",        "demo-node-001", 0);
        var laptop      = NewNode(SpaceNodeId, "笔记本电脑",  "demo-node-002", 1);
        var desk        = NewNode(SpaceNodeId, "办公桌",      "demo-node-003", 2);
        var iphone      = NewNode("demo-node-001", "iPhone 15 Pro", "demo-node-004", 0);

        db.StringTreeSkyNodes.AddRange(space, phone, laptop, desk, iphone);
        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 3：选项集（priority / status，挂在 demo EntityType）
        // ════════════════════════════════════════════════════
        var prioritySet = await EnsureOptionSetAsync(db, SpaceEntityType, "priority", "优先级",
            new (string Value, string Label, int Order, bool IsDefault)[]
            {
                ("low",    "低",   10, false),
                ("normal", "普通", 20, true),
                ("high",   "高",   30, false),
                ("urgent", "紧急", 40, false),
            }, ct);

        var statusSet = await EnsureOptionSetAsync(db, SpaceEntityType, "status", "状态",
            new (string Value, string Label, int Order, bool IsDefault)[]
            {
                ("draft",    "草稿",   10, true),
                ("active",   "进行中", 20, false),
                ("archived", "已归档", 30, false),
            }, ct);

        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 4：组合类型 Specs（color + weight + brand）
        // ════════════════════════════════════════════════════
        var brandType = new CompositeTypeDefinition
        {
            EntityType  = SpaceEntityType,
            TypeName    = "Brand",
            DisplayName = "品牌",
            Fields =
            {
                new CompositeFieldDefinition
                {
                    FieldName = "name", DisplayName = "品牌名",
                    DataType = EavDataTypes.String, IsSearchable = true, DisplayOrder = 1
                },
                new CompositeFieldDefinition
                {
                    FieldName = "origin", DisplayName = "产地",
                    DataType = EavDataTypes.String, DisplayOrder = 2
                }
            }
        };
        db.CompositeTypes.Add(brandType);
        await db.SaveChangesAsync(ct);

        var specsType = new CompositeTypeDefinition
        {
            EntityType  = SpaceEntityType,
            TypeName    = "Specs",
            DisplayName = "规格参数",
            Fields =
            {
                new CompositeFieldDefinition
                {
                    FieldName = "color", DisplayName = "颜色",
                    DataType = EavDataTypes.String, IsSearchable = true, DisplayOrder = 1
                },
                new CompositeFieldDefinition
                {
                    FieldName = "weight", DisplayName = "重量(kg)",
                    DataType = EavDataTypes.Decimal, DisplayOrder = 2,
                    UnitId   = KgUnitId
                },
                new CompositeFieldDefinition
                {
                    FieldName = "brand", DisplayName = "品牌",
                    DataType = EavDataTypes.Composite,
                    RefCompositeTypeId = brandType.CompositeTypeId,
                    IsSearchable = true, DisplayOrder = 3
                }
            }
        };
        db.CompositeTypes.Add(specsType);
        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 5：自定义表 certifications
        // ════════════════════════════════════════════════════
        var certsTable = new CustomTableDefinition
        {
            EntityType  = SpaceEntityType,
            TableName   = "certifications",
            DisplayName = "认证证书",
            DisplayOrder = 1
        };
        certsTable.Columns.Add(new CustomTableColumn
        {
            ColumnName = "cert_name", DisplayName = "证书名称",
            DataType = EavDataTypes.String,
            IsRequired = true, IsSearchable = true, IsUnique = true, DisplayOrder = 1
        });
        certsTable.Columns.Add(new CustomTableColumn
        {
            ColumnName = "issuer", DisplayName = "颁发机构",
            DataType = EavDataTypes.String, IsSearchable = true, DisplayOrder = 2
        });
        certsTable.Columns.Add(new CustomTableColumn
        {
            ColumnName = "issued_date", DisplayName = "颁发日期",
            DataType = EavDataTypes.Date, DisplayOrder = 3
        });
        db.CustomTables.Add(certsTable);
        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 6：属性定义（覆盖 13 种 DataType）
        // ════════════════════════════════════════════════════
        var titleAttr    = await EnsureAttributeAsync(db, SpaceEntityType, "title",       "标题",     EavDataTypes.String,       true,  true,  true,  10, null,      null, ct);
        var descAttr     = await EnsureAttributeAsync(db, SpaceEntityType, "description", "描述",     EavDataTypes.String,       false, true,  false, 20, null,      null, ct);
        var qtyAttr      = await EnsureAttributeAsync(db, SpaceEntityType, "quantity",    "数量",     EavDataTypes.Int,          false, true,  true,  30, "1",       null, ct);
        var priceAttr    = await EnsureAttributeAsync(db, SpaceEntityType, "price",       "价格",     EavDataTypes.Decimal,      true,  true,  true,  40, null,      null, ct);
        var weightAttr   = await EnsureAttributeAsync(db, SpaceEntityType, "weight",      "净重",     EavDataTypes.Decimal,      false, true,  true,  50, null,      null, ct);
        weightAttr.UnitId = KgUnitId;
        var activeAttr   = await EnsureAttributeAsync(db, SpaceEntityType, "isActive",    "是否启用", EavDataTypes.Bool,         false, false, true,  60, "true",    null, ct);
        var createdAtAttr= await EnsureAttributeAsync(db, SpaceEntityType, "createdAt",   "创建时间", EavDataTypes.Datetime,      false, true,  true,  70, null,      null, ct);
        var pubDateAttr  = await EnsureAttributeAsync(db, SpaceEntityType, "publishDate", "发布日期", EavDataTypes.Date,         false, true,  true,  80, null,      null, ct);
        var workTimeAttr = await EnsureAttributeAsync(db, SpaceEntityType, "workTime",    "工作时间", EavDataTypes.Time,         false, false, false, 90, null,      null, ct);
        var metaAttr     = await EnsureAttributeAsync(db, SpaceEntityType, "metadata",    "元数据",   EavDataTypes.Json,         false, false, false, 100, null,     null, ct);
        var priorityAttr = await EnsureAttributeAsync(db, SpaceEntityType, "priority",    "优先级",   EavDataTypes.SingleChoice, false, true,  true,  110, "normal", prioritySet.OptionSetId, ct);
        var statusAttr   = await EnsureAttributeAsync(db, SpaceEntityType, "status",      "状态",     EavDataTypes.SingleChoice, false, true,  true,  120, "draft",  statusSet.OptionSetId, ct);

        // composite + table 需在最后补（依赖前面创建的 compositeType / table）
        var specsAttr = await EnsureAttributeAsync(db, SpaceEntityType, "specs", "规格参数",
            EavDataTypes.Composite, false, true, false, 130, null, null, ct);
        specsAttr.RefCompositeTypeId = specsType.CompositeTypeId;

        var certsAttr = await EnsureAttributeAsync(db, SpaceEntityType, "certifications", "认证证书",
            EavDataTypes.Table, false, true, false, 140, null, null, ct);
        certsAttr.RefTableDefinitionId = certsTable.TableDefinitionId;

        // ★ file 属性 attachment：仅存元数据，不依赖 FileStorageApi 二进制实体
        var attachAttr = await EnsureAttributeAsync(db, SpaceEntityType, "attachment", "附件",
            EavDataTypes.File, false, false, false, 150, null, null, ct);

        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 7：示例属性值（3 个子节点 + 1 个孙节点写完整值）
        // ════════════════════════════════════════════════════
        // ── demo-node-001 手机 ──
        await UpsertStringAsync  (db, "demo-node-001", SpaceEntityType, titleAttr,     "旗舰手机", ct);
        await UpsertStringAsync  (db, "demo-node-001", SpaceEntityType, descAttr,      "5G 智能手机，旗舰配置", ct);
        await UpsertIntAsync     (db, "demo-node-001", SpaceEntityType, qtyAttr,       100, ct);
        await UpsertDecimalAsync (db, "demo-node-001", SpaceEntityType, priceAttr,     5999.00m, null, ct);
        await UpsertDecimalAsync (db, "demo-node-001", SpaceEntityType, weightAttr,    0.21m, KgUnitId, ct);
        await UpsertBoolAsync    (db, "demo-node-001", SpaceEntityType, activeAttr,    true, ct);
        await UpsertDatetimeAsync(db, "demo-node-001", SpaceEntityType, createdAtAttr,
            new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero), ct);
        await UpsertDateAsync    (db, "demo-node-001", SpaceEntityType, pubDateAttr,
            new DateOnly(2026, 2, 1), ct);
        await UpsertTimeAsync    (db, "demo-node-001", SpaceEntityType, workTimeAttr,
            new TimeOnly(9, 0, 0), ct);
        await UpsertStringAsync  (db, "demo-node-001", SpaceEntityType, priorityAttr,   "high", ct);
        await UpsertStringAsync  (db, "demo-node-001", SpaceEntityType, statusAttr,     "active", ct);
        await UpsertJsonAsync    (db, "demo-node-001", SpaceEntityType, metaAttr,
            JsonDocument.Parse("""{"color":"黑色","storage":"256GB"}"""), ct);
        await UpsertCompositeAsync(db, "demo-node-001", SpaceEntityType, specsAttr,
            JsonDocument.Parse("""{"color":"午夜黑","weight":0.21,"brand":{"name":"Apple","origin":"美国"}}"""), ct);
        await UpsertFileAsync(db, "demo-node-001", SpaceEntityType, attachAttr,
            BuildFileMeta("demo-doc-001", "手机使用说明书.pdf",
                "application/pdf", 524288, "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90"), ct);

        // ── demo-node-002 笔记本电脑 ──
        await UpsertStringAsync  (db, "demo-node-002", SpaceEntityType, titleAttr,     "超薄笔记本", ct);
        await UpsertStringAsync  (db, "demo-node-002", SpaceEntityType, descAttr,      "16 英寸轻薄笔记本", ct);
        await UpsertIntAsync     (db, "demo-node-002", SpaceEntityType, qtyAttr,       50, ct);
        await UpsertDecimalAsync (db, "demo-node-002", SpaceEntityType, priceAttr,     12999.00m, null, ct);
        await UpsertDecimalAsync (db, "demo-node-002", SpaceEntityType, weightAttr,    1.8m, KgUnitId, ct);
        await UpsertBoolAsync    (db, "demo-node-002", SpaceEntityType, activeAttr,    true, ct);
        await UpsertDatetimeAsync(db, "demo-node-002", SpaceEntityType, createdAtAttr,
            new DateTimeOffset(2026, 3, 10, 14, 30, 0, TimeSpan.Zero), ct);
        await UpsertDateAsync    (db, "demo-node-002", SpaceEntityType, pubDateAttr,
            new DateOnly(2026, 3, 20), ct);
        await UpsertTimeAsync    (db, "demo-node-002", SpaceEntityType, workTimeAttr,
            new TimeOnly(10, 0, 0), ct);
        await UpsertStringAsync  (db, "demo-node-002", SpaceEntityType, priorityAttr,   "normal", ct);
        await UpsertStringAsync  (db, "demo-node-002", SpaceEntityType, statusAttr,     "active", ct);
        await UpsertJsonAsync    (db, "demo-node-002", SpaceEntityType, metaAttr,
            JsonDocument.Parse("""{"cpu":"M3","ram":"16GB","disk":"512GB SSD"}"""), ct);
        await UpsertCompositeAsync(db, "demo-node-002", SpaceEntityType, specsAttr,
            JsonDocument.Parse("""{"color":"深空灰","weight":1.8,"brand":{"name":"Apple","origin":"美国"}}"""), ct);
        await UpsertFileAsync(db, "demo-node-002", SpaceEntityType, attachAttr,
            BuildFileMeta("demo-doc-002", "笔记本电脑保修卡.pdf",
                "application/pdf", 262144, "b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2"), ct);

        // ── demo-node-003 办公桌 ──
        await UpsertStringAsync  (db, "demo-node-003", SpaceEntityType, titleAttr,     "实木办公桌", ct);
        await UpsertStringAsync  (db, "demo-node-003", SpaceEntityType, descAttr,      "1.4m 实木办公桌，含抽屉", ct);
        await UpsertIntAsync     (db, "demo-node-003", SpaceEntityType, qtyAttr,       20, ct);
        await UpsertDecimalAsync (db, "demo-node-003", SpaceEntityType, priceAttr,     1899.00m, null, ct);
        await UpsertDecimalAsync (db, "demo-node-003", SpaceEntityType, weightAttr,    35.5m, KgUnitId, ct);
        await UpsertBoolAsync    (db, "demo-node-003", SpaceEntityType, activeAttr,    false, ct);
        await UpsertDatetimeAsync(db, "demo-node-003", SpaceEntityType, createdAtAttr,
            new DateTimeOffset(2026, 5, 5, 8, 0, 0, TimeSpan.Zero), ct);
        await UpsertDateAsync    (db, "demo-node-003", SpaceEntityType, pubDateAttr,
            new DateOnly(2026, 5, 10), ct);
        await UpsertTimeAsync    (db, "demo-node-003", SpaceEntityType, workTimeAttr,
            new TimeOnly(8, 30, 0), ct);
        await UpsertStringAsync  (db, "demo-node-003", SpaceEntityType, priorityAttr,   "low", ct);
        await UpsertStringAsync  (db, "demo-node-003", SpaceEntityType, statusAttr,     "draft", ct);
        await UpsertJsonAsync    (db, "demo-node-003", SpaceEntityType, metaAttr,
            JsonDocument.Parse("""{"material":"橡木","dimensions":"140x70x75cm"}"""), ct);
        await UpsertCompositeAsync(db, "demo-node-003", SpaceEntityType, specsAttr,
            JsonDocument.Parse("""{"color":"原木色","weight":35.5,"brand":{"name":"IKEA","origin":"瑞典"}}"""), ct);
        await UpsertFileAsync(db, "demo-node-003", SpaceEntityType, attachAttr,
            BuildFileMeta("demo-doc-003", "办公桌安装图.jpg",
                "image/jpeg", 1572864, "c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4"), ct);

        // ── demo-node-004 孙节点 iPhone 15 Pro（与 001/002/003 同 EntityType，独立值集合） ──
        await UpsertStringAsync  (db, "demo-node-004", SpaceEntityType, titleAttr,     "iPhone 15 Pro", ct);
        await UpsertStringAsync  (db, "demo-node-004", SpaceEntityType, descAttr,      "钛金属外壳，A17 Pro 芯片", ct);
        await UpsertIntAsync     (db, "demo-node-004", SpaceEntityType, qtyAttr,       30, ct);
        await UpsertDecimalAsync (db, "demo-node-004", SpaceEntityType, priceAttr,     7999.00m, null, ct);
        await UpsertDecimalAsync (db, "demo-node-004", SpaceEntityType, weightAttr,    0.187m, KgUnitId, ct);
        await UpsertBoolAsync    (db, "demo-node-004", SpaceEntityType, activeAttr,    true, ct);
        await UpsertDatetimeAsync(db, "demo-node-004", SpaceEntityType, createdAtAttr,
            new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero), ct);
        await UpsertDateAsync    (db, "demo-node-004", SpaceEntityType, pubDateAttr,
            new DateOnly(2026, 9, 15), ct);
        await UpsertTimeAsync    (db, "demo-node-004", SpaceEntityType, workTimeAttr,
            new TimeOnly(9, 30, 0), ct);
        await UpsertStringAsync  (db, "demo-node-004", SpaceEntityType, priorityAttr,   "urgent", ct);
        await UpsertStringAsync  (db, "demo-node-004", SpaceEntityType, statusAttr,     "active", ct);
        await UpsertJsonAsync    (db, "demo-node-004", SpaceEntityType, metaAttr,
            JsonDocument.Parse("""{"color":"钛色","chip":"A17 Pro","storage":"256GB"}"""), ct);
        await UpsertCompositeAsync(db, "demo-node-004", SpaceEntityType, specsAttr,
            JsonDocument.Parse("""{"color":"钛色","weight":0.187,"brand":{"name":"Apple","origin":"美国"}}"""), ct);
        await UpsertFileAsync(db, "demo-node-004", SpaceEntityType, attachAttr,
            BuildFileMeta("demo-doc-004", "iPhone钛金属特写.png",
                "image/png", 2097152, "d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5"), ct);

        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════
        // 步骤 8：自定义表行数据（demo-node-001 的两张证书）
        // ⚠️ CustomTableRow 实际字段：RowData (JsonDocument) / RowOrder (int) / AttributeId
        //    AttributeId 必须指向 demo EntityType 上声明 table 类型的那个属性
        //    （certsAttr），CustomTableWriteService 已验证此为强约束。
        // ════════════════════════════════════════════════════
        await UpsertTableRowAsync(db, "demo-node-001", SpaceEntityType,
            certsTable.TableDefinitionId, certsAttr.AttributeId, 0,
            new (string ColumnName, object Value)[]
            {
                ("cert_name",   "3C 认证"),
                ("issuer",      "中国质量认证中心"),
                ("issued_date", new DateOnly(2026, 1, 10))
            }, ct);

        await UpsertTableRowAsync(db, "demo-node-001", SpaceEntityType,
            certsTable.TableDefinitionId, certsAttr.AttributeId, 1,
            new (string ColumnName, object Value)[]
            {
                ("cert_name",   "CE 认证"),
                ("issuer",      "欧盟标准委员会"),
                ("issued_date", new DateOnly(2026, 1, 12))
            }, ct);

        await db.SaveChangesAsync(ct);

        // ════════════════════════════════════════════════════════════════════
        // 步骤 9：第二个 Space（demo-space-002）—— 验证两个 Space 的 EntityType 独占
        //
        // 设计要点：
        //   ★ 共享：与 demo-space-001 同样遵循 SpaceController 命名约定 StringTreeNode:{spaceId}
        //   ★ 独占：
        //     - 第二个 Space 有独立的 EntityType（demo-space-002）
        //     - 同名属性（title/quantity/priority/status/weight）在两个 EntityType 下
        //       是不同的 AttributeDefinition 行（不同 AttributeId、不同 EntityType）
        //     - 选项集 priority 在两个 EntityType 下也是不同 OptionSet
        //     - 属性值写入互不干扰（同 EntityId 在不同 EntityType 下是不同行）
        //
        // 为简化对比，第二个 Space 只创建 5 个核心属性（其中 4 个与 demo-space-001 同名），
        // 并写 2 个子节点的示例值。
        // ════════════════════════════════════════════════════════════════════

        // 9.1 注册第二个 EntityType
        await EnsureEntityTypeAsync(db, SecondSpaceEntityType, "演示空间二",
            "用于验证 EAV 属性类型的独占性", 200, ct);
        await db.SaveChangesAsync(ct);

        // 9.2 第二个 Space 的根节点 + 2 个子节点（共 3 个，满足"最少三个 node"）
        var space2 = new StringNodeEntity
        {
            Id          = SecondSpaceNodeId,
            Name        = "演示空间二",
            ParentId    = null,
            Description = "验证 EAV 独占性的第二个 Space",
            SortOrder   = 200,
            EntityType  = SecondSpaceEntityType
        };
        var space2Child1 = NewSecondNode(SecondSpaceNodeId, "智能手表",  "demo-node-101", 0);
        var space2Child2 = NewSecondNode(SecondSpaceNodeId, "平板电脑", "demo-node-102", 1);
        db.StringTreeSkyNodes.AddRange(space2, space2Child1, space2Child2);
        await db.SaveChangesAsync(ct);

        // 9.3 第二个 EntityType 下的选项集 priority（与 demo-space-001 同名但独立）
        var prioritySet2 = await EnsureOptionSetAsync(
            db, SecondSpaceEntityType, "priority", "优先级",
            new (string Value, string Label, int Order, bool IsDefault)[]
            {
                ("p1", "P1", 10, false),
                ("p2", "P2", 20, true),
                ("p3", "P3", 30, false),
            }, ct);
        await db.SaveChangesAsync(ct);

        // 9.4 第二个 EntityType 下的属性定义
        //     其中 title/quantity/priority/status/weight 与 demo-space-001 同名
        //     → 验证"同名属性在两个 EntityType 下是不同 AttributeDefinition"
        var title2    = await EnsureAttributeAsync(db, SecondSpaceEntityType, "title",    "标题",   EavDataTypes.String,       true,  true,  true,  10, null,      null, ct);
        var qty2      = await EnsureAttributeAsync(db, SecondSpaceEntityType, "quantity", "数量",   EavDataTypes.Int,          false, true,  true,  20, "1",       null, ct);
        var weight2   = await EnsureAttributeAsync(db, SecondSpaceEntityType, "weight",   "净重",   EavDataTypes.Decimal,      false, true,  true,  30, null,      null, ct);
        weight2.UnitId = KgUnitId;
        var priority2 = await EnsureAttributeAsync(db, SecondSpaceEntityType, "priority", "优先级", EavDataTypes.SingleChoice, false, true,  true,  40, "p2",      prioritySet2.OptionSetId, ct);
        var status2   = await EnsureAttributeAsync(db, SecondSpaceEntityType, "status",   "状态",   EavDataTypes.SingleChoice, false, true,  true,  50, "draft",   null, ct);
        // status2 不引用选项集，验证"两个 Space 的同名属性可以有不同的引用配置"
        await db.SaveChangesAsync(ct);

        // 9.5 第二个 Space 节点的属性值
        //     demo-node-101 智能手表
        await UpsertStringAsync (db, "demo-node-101", SecondSpaceEntityType, title2,    "Galaxy Watch", ct);
        await UpsertIntAsync    (db, "demo-node-101", SecondSpaceEntityType, qty2,      80, ct);
        await UpsertDecimalAsync(db, "demo-node-101", SecondSpaceEntityType, weight2,   0.05m, KgUnitId, ct);
        await UpsertStringAsync (db, "demo-node-101", SecondSpaceEntityType, priority2, "p1", ct);
        await UpsertStringAsync (db, "demo-node-101", SecondSpaceEntityType, status2,   "draft", ct);

        //     demo-node-102 平板电脑
        await UpsertStringAsync (db, "demo-node-102", SecondSpaceEntityType, title2,    "iPad Air", ct);
        await UpsertIntAsync    (db, "demo-node-102", SecondSpaceEntityType, qty2,      40, ct);
        await UpsertDecimalAsync(db, "demo-node-102", SecondSpaceEntityType, weight2,   0.46m, KgUnitId, ct);
        await UpsertStringAsync (db, "demo-node-102", SecondSpaceEntityType, priority2, "p2", ct);
        await UpsertStringAsync (db, "demo-node-102", SecondSpaceEntityType, status2,   "draft", ct);

        await db.SaveChangesAsync(ct);
    }

    // ════════════════════════════════════════════════════════
    // 私有 helper
    // ════════════════════════════════════════════════════════

    private static StringNodeEntity NewNode(
        string parentId, string name, string id, int sortOrder) => new()
    {
        Id          = id,
        Name        = name,
        ParentId    = parentId,
        SortOrder   = sortOrder,
        EntityType  = SpaceEntityType
    };

    /// <summary>为第二个 Space（demo-space-002）创建子节点，EntityType 用 SecondSpaceEntityType。</summary>
    private static StringNodeEntity NewSecondNode(
        string parentId, string name, string id, int sortOrder) => new()
    {
        Id          = id,
        Name        = name,
        ParentId    = parentId,
        SortOrder   = sortOrder,
        EntityType  = SecondSpaceEntityType
    };

    private static async Task<EntityTypeDefinition> EnsureEntityTypeAsync(
        TreeGraphDbContext db, string code, string displayName, string? description,
        int displayOrder, CancellationToken ct)
    {
        var existing = await db.EntityTypes
            .FirstOrDefaultAsync(t => t.EntityType == code && !t.IsDeleted, ct);
        if (existing != null) return existing;

        var entity = new EntityTypeDefinition
        {
            EntityType   = code,
            DisplayName  = displayName,
            Description  = description,
            DisplayOrder = displayOrder
        };
        db.EntityTypes.Add(entity);
        await db.SaveChangesAsync(ct);
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
            EntityType  = entityType,
            SetName     = setName,
            DisplayName = displayName
        };
        foreach (var (value, label, order, isDefault) in items)
        {
            set.Items.Add(new OptionItem
            {
                Value        = value,
                Label        = label,
                DisplayOrder = order,
                IsDefault    = isDefault
            });
        }
        db.OptionSets.Add(set);
        await db.SaveChangesAsync(ct);
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
            EntityType      = entityType,
            AttributeName   = attributeName,
            DisplayName    = displayName,
            DataType        = dataType,
            IsRequired     = isRequired,
            IsSearchable   = isSearchable,
            IsSortable     = isSortable,
            DisplayOrder   = displayOrder,
            DefaultValue   = defaultValue,
            RefOptionSetId = refOptionSetId,
            Version        = 1
        };
        db.AttributeCatalog.Add(attr);
        await db.SaveChangesAsync(ct);
        return attr;
    }

    // ---------- Upsert 属性值（按 EntityId + AttributeId 幂等） ----------

    private static async Task UpsertStringAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, string value, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;
        db.AttributeValues.Add(new AttributeValue
        {
            EntityId    = entityId,
            EntityType  = entityType,
            AttributeId = attr.AttributeId,
            ValueString = value
        });
    }

    private static async Task UpsertIntAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, long value, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;
        db.AttributeValues.Add(new AttributeValue
        {
            EntityId   = entityId,
            EntityType = entityType,
            AttributeId= attr.AttributeId,
            ValueInt   = value
        });
    }

    private static async Task UpsertDecimalAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, decimal value, Guid? unitId, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;
        db.AttributeValues.Add(new AttributeValue
        {
            EntityId    = entityId,
            EntityType  = entityType,
            AttributeId = attr.AttributeId,
            ValueDecimal= value,
            UnitId      = unitId
        });
    }

    private static async Task UpsertBoolAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, bool value, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;
        db.AttributeValues.Add(new AttributeValue
        {
            EntityId   = entityId,
            EntityType = entityType,
            AttributeId= attr.AttributeId,
            ValueBool  = value
        });
    }

    private static async Task UpsertDatetimeAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, DateTimeOffset value, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;
        db.AttributeValues.Add(new AttributeValue
        {
            EntityId      = entityId,
            EntityType    = entityType,
            AttributeId   = attr.AttributeId,
            ValueDatetime = value
        });
    }

    private static async Task UpsertDateAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, DateOnly value, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;
        db.AttributeValues.Add(new AttributeValue
        {
            EntityId     = entityId,
            EntityType   = entityType,
            AttributeId  = attr.AttributeId,
            ValueDateOnly= value
        });
    }

    private static async Task UpsertTimeAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, TimeOnly value, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;
        db.AttributeValues.Add(new AttributeValue
        {
            EntityId  = entityId,
            EntityType= entityType,
            AttributeId = attr.AttributeId,
            ValueTime = value
        });
    }

    private static async Task UpsertJsonAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, JsonDocument value, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;
        db.AttributeValues.Add(new AttributeValue
        {
            EntityId    = entityId,
            EntityType  = entityType,
            AttributeId = attr.AttributeId,
            ValueJsonb   = JsonDocument.Parse(value.RootElement.GetRawText())
        });
    }

    private static async Task UpsertCompositeAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, JsonDocument value, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;
        // composite 类型也存 ValueJsonb（与 EavWriteService 一致）
        db.AttributeValues.Add(new AttributeValue
        {
            EntityId   = entityId,
            EntityType = entityType,
            AttributeId= attr.AttributeId,
            ValueJsonb  = JsonDocument.Parse(value.RootElement.GetRawText())
        });
    }

    // ── file 属性：ValueFileMeta 存文件元数据 JSON（与 FileMetadataDto 对齐） ──
    //   注意：seeder 只构造元数据，不调用 FileStorageApi 二进制上下传。
    //   真正的上下传集成测试在 FileStorageApiTests 项目（需 SeaweedFS 容器）。
    private static async Task UpsertFileAsync(
        TreeGraphDbContext db, string entityId, string entityType,
        AttributeDefinition attr, JsonDocument fileMeta, CancellationToken ct)
    {
        if (await db.AttributeValues.AnyAsync(
                v => v.EntityId == entityId && v.EntityType == entityType
                  && v.AttributeId == attr.AttributeId, ct))
            return;

        db.AttributeValues.Add(new AttributeValue
        {
            EntityId      = entityId,
            EntityType    = entityType,
            AttributeId   = attr.AttributeId,
            ValueFileMeta = JsonDocument.Parse(fileMeta.RootElement.GetRawText())
        });
    }

    /// <summary>
    /// 构造文件元数据 JSON（与 TreeGraph.Shared/FileStorageSky/Contracts/FileMetadataDto 对齐）。
    /// 字段：docId / fileName / contentType / size / sha256 / version / status。
    /// </summary>
    private static JsonDocument BuildFileMeta(
        string docId, string fileName, string contentType, long size, string sha256)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            docId,
            fileName,
            contentType,
            size,
            sha256,
            version = 1,
            status = "active"
        }));
    }

    private static async Task UpsertTableRowAsync(
        TreeGraphDbContext db,
        string parentEntityId, string parentEntityType,
        string tableDefinitionId, string attributeId, int rowOrder,
        (string ColumnName, object Value)[] cells,
        CancellationToken ct)
    {
        // 幂等：父实体 + 表 + 行序已有则跳过
        if (await db.CustomTableRows.AnyAsync(
                r => r.ParentEntityType  == parentEntityType
                  && r.ParentEntityId    == parentEntityId
                  && r.TableDefinitionId == tableDefinitionId
                  && r.RowOrder          == rowOrder, ct))
            return;

        // 构造 RowData JSON：{ "col1": "v1", "col2": "v2", ... }
        var rowDict = new Dictionary<string, object?>();
        foreach (var (columnName, value) in cells)
        {
            rowDict[columnName] = value switch
            {
                DateOnly d       => d.ToString("yyyy-MM-dd"),
                DateTime dt      => dt,
                DateTimeOffset dto => dto,
                bool b           => b,
                int i            => i,
                long l           => l,
                decimal dec      => dec,
                _               => value.ToString()
            };
        }

        var row = new CustomTableRow
        {
            ParentEntityType  = parentEntityType,
            ParentEntityId    = parentEntityId,
            TableDefinitionId = tableDefinitionId,
            AttributeId       = attributeId,
            RowOrder          = rowOrder,
            RowData           = JsonDocument.Parse(JsonSerializer.Serialize(rowDict))
        };

        db.CustomTableRows.Add(row);
    }
}
