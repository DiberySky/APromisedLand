using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.Eav;

namespace TreeGraph.Api.NodeEavSky.Data.Seeding;

/// <summary>
/// 示例元数据种子：组合类型 Specs + Brand、Product 属性目录、
/// 自定义表 certifications、选项集 gender / quality_grade。
///
/// 幂等保证：
///   - 严格的多表存在性检查（不是只查 AttributeCatalog）
///   - 整个 seed 包裹在事务中（含 execution strategy 重试兼容）
///   - 失败时事务回滚，不会留下部分数据
///
/// 依赖：UnitSeedService 已先行写入单位（net_weight 需要按符号查 kg 的 Id）。
/// </summary>
public static class EavSeeder
{
    public static async Task SeedAsync(EavDbContext db, CancellationToken ct = default)
    {
        // 补 EntityTypeCatalog：即使旧数据已有 Product 属性，也能补齐类型记录
        await EnsureEntityTypesAsync(db, ct);

        // ── 严格的幂等检查：所有关键实体都存在才跳过 ──
        if (await IsAlreadySeededAsync(db, ct))
            return;

        // ── Npgsql 重试策略兼容：用 ExecutionStrategy 包裹整个 seed ──
        // （裸 BeginTransactionAsync 在 EnableRetryOnFailure 下会抛
        //   "execution strategy does not support user-initiated transactions"）
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

    /// <summary>补 EntityTypeCatalog：即使旧数据已有 Product 属性，也能补齐类型记录。</summary>
    private static async Task EnsureEntityTypesAsync(EavDbContext db, CancellationToken ct)
    {
        if (await db.EntityTypes.AnyAsync(t => t.EntityType == "Product", ct))
            return;

        db.EntityTypes.Add(new EntityTypeDefinition
        {
            EntityType = "Product",
            DisplayName = "商品",
            Description = "示例：商品实体类型",
            DisplayOrder = 1
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 严格幂等检查：以下相关记录都存在才认为已 seed。
    /// 注意：同一 DbContext 不支持并发查询，必须顺序执行（不能 Task.WhenAll）。
    /// </summary>
    private static async Task<bool> IsAlreadySeededAsync(
        EavDbContext db, CancellationToken ct)
    {
        return await db.AttributeCatalog.AnyAsync(a => a.EntityType == "Product", ct)
            && await db.CompositeTypes.AnyAsync(
                t => t.EntityType == "Product" && t.TypeName == "Specs", ct)
            && await db.CompositeTypes.AnyAsync(
                t => t.EntityType == "Product" && t.TypeName == "Brand", ct)
            && await db.CustomTables.AnyAsync(
                t => t.EntityType == "Product" && t.TableName == "certifications", ct)
            && await db.OptionSets.AnyAsync(
                s => s.EntityType == "Shared" && s.SetName == "gender", ct)
            && await db.OptionSets.AnyAsync(
                s => s.EntityType == "Product" && s.SetName == "quality_grade", ct);
    }

    /// <summary>实际的 seed 逻辑（在事务中执行，中途 SaveChanges 不落库，Commit 才生效）</summary>
    private static async Task SeedInternalAsync(EavDbContext db, CancellationToken ct)
    {
        // ============================================================
        // 步骤 1：组合类型 Brand（先建，Specs 要引用它）
        // ============================================================
        var brand = new CompositeTypeDefinition
        {
            EntityType = "Product",
            TypeName = "Brand",
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
        db.CompositeTypes.Add(brand);
        await db.SaveChangesAsync(ct);
        // 此时 brand.CompositeTypeId 已生成

        // ============================================================
        // 步骤 2：组合类型 Specs（直接引用 brand，消除两阶段回填）
        // ============================================================
        var specs = new CompositeTypeDefinition
        {
            EntityType = "Product",
            TypeName = "Specs",
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
                    DataType = EavDataTypes.Decimal, DisplayOrder = 2
                },
                new CompositeFieldDefinition
                {
                    FieldName = "brand", DisplayName = "品牌信息",
                    DataType = EavDataTypes.Composite,
                    RefCompositeTypeId = brand.CompositeTypeId, // ← 直接引用
                    IsSearchable = true, DisplayOrder = 3
                }
            }
        };
        db.CompositeTypes.Add(specs);
        await db.SaveChangesAsync(ct);

        // ============================================================
        // 步骤 3：查 kg 单位（UnitSeedService 已先执行）
        // ============================================================
        var kgUnitId = await db.Units
            .Where(u => u.Category == "weight" && u.Symbol == "kg")
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (kgUnitId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "kg 单位未找到。请确认 UnitSeedService 已在 EavSeeder 之前执行。");
        }

        // ============================================================
        // 步骤 4：Product 属性目录（4 个基础属性 + 2 个引用属性）
        // ============================================================
        db.AttributeCatalog.AddRange(
            new AttributeDefinition
            {
                EntityType = "Product",
                AttributeName = "screen_size",
                DisplayName = "屏幕尺寸",
                DataType = EavDataTypes.Decimal,
                IsRequired = true,
                IsSearchable = true,
                IsSortable = true,
                DisplayOrder = 1,
                ValidationRule = JsonDocument.Parse("""{"min": 0, "max": 200}""")
            },
            new AttributeDefinition
            {
                EntityType = "Product",
                AttributeName = "release_date",
                DisplayName = "发布日期",
                DataType = EavDataTypes.Date,
                IsSearchable = true,
                DisplayOrder = 2
            },
            new AttributeDefinition
            {
                EntityType = "Product",
                AttributeName = "net_weight",
                DisplayName = "净重",
                DataType = EavDataTypes.Decimal,
                IsSearchable = true,
                IsSortable = true,
                DisplayOrder = 3,
                UnitId = kgUnitId, // 基准单位：千克
                ValidationRule = JsonDocument.Parse("""{"min": 0, "max": 10000}""")
            },
            new AttributeDefinition
            {
                EntityType = "Product",
                AttributeName = "specs",
                DisplayName = "规格参数",
                DataType = EavDataTypes.Composite,
                IsSearchable = true,
                DisplayOrder = 4,
                RefCompositeTypeId = specs.CompositeTypeId
            });

        // ============================================================
        // 步骤 5：自定义表 certifications
        // ============================================================
        var certs = new CustomTableDefinition
        {
            EntityType = "Product",
            TableName = "certifications",
            DisplayName = "认证证书",
            DisplayOrder = 1
        };
        certs.Columns.Add(new CustomTableColumn
        {
            ColumnName = "cert_name", DisplayName = "证书名称",
            DataType = EavDataTypes.String,
            IsRequired = true, IsSearchable = true, IsUnique = true, DisplayOrder = 1
        });
        certs.Columns.Add(new CustomTableColumn
        {
            ColumnName = "issuer", DisplayName = "颁发机构",
            DataType = EavDataTypes.String, IsSearchable = true, DisplayOrder = 2
        });
        certs.Columns.Add(new CustomTableColumn
        {
            ColumnName = "issued_date", DisplayName = "颁发日期",
            DataType = EavDataTypes.Date, DisplayOrder = 3
        });
        db.CustomTables.Add(certs);
        await db.SaveChangesAsync(ct);

        db.AttributeCatalog.Add(new AttributeDefinition
        {
            EntityType = "Product",
            AttributeName = "certifications",
            DisplayName = "认证证书",
            DataType = EavDataTypes.Table,
            IsSearchable = true,
            DisplayOrder = 5,
            RefTableDefinitionId = certs.TableDefinitionId
        });

        // ============================================================
        // 步骤 6：选项集 gender（Shared）+ quality_grade（Product）
        // ============================================================
        var gender = new OptionSet
        {
            EntityType = "Shared", SetName = "gender", DisplayName = "性别"
        };
        gender.Items.Add(new OptionItem { Value = "unknown", Label = "未知", DisplayOrder = 1, IsDefault = true });
        gender.Items.Add(new OptionItem { Value = "male", Label = "男", DisplayOrder = 2 });
        gender.Items.Add(new OptionItem { Value = "female", Label = "女", DisplayOrder = 3 });
        gender.Items.Add(new OptionItem { Value = "other", Label = "其他", DisplayOrder = 4 });
        db.OptionSets.Add(gender);

        var grade = new OptionSet
        {
            EntityType = "Product", SetName = "quality_grade", DisplayName = "质量等级"
        };
        grade.Items.Add(new OptionItem { Value = "grade_a", Label = "一级", DisplayOrder = 1 });
        grade.Items.Add(new OptionItem { Value = "grade_b", Label = "二级", DisplayOrder = 2 });
        grade.Items.Add(new OptionItem { Value = "grade_c", Label = "三级", DisplayOrder = 3 });
        db.OptionSets.Add(grade);
        await db.SaveChangesAsync(ct);

        db.AttributeCatalog.Add(new AttributeDefinition
        {
            EntityType = "Product",
            AttributeName = "quality_grade",
            DisplayName = "质量等级",
            DataType = EavDataTypes.SingleChoice,
            IsSearchable = true,
            DisplayOrder = 6,
            RefOptionSetId = grade.OptionSetId
        });

        // 最终一次性提交（之前每步 SaveChanges 已在事务内，最后一次统一落库）
        await db.SaveChangesAsync(ct);
    }
}
