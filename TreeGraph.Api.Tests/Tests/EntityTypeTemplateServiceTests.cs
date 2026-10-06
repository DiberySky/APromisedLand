using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.StringTreeSky.Services;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// EntityType 属性模板复制服务测试。
///
/// 适配说明（相对设计文档）：
///   文档原测试用 EF InMemory，但本仓库测试项目统一使用 Testcontainers
///   PostgreSQL（Integration 集合，全测试共享容器），且不引入 InMemory 包。
///   因此：
///     1. TreeGraphDbContext 从共享容器的 DI 容器解析（每测一个 scope）；
///     2. 每个测试用 GUID 后缀的唯一 EntityType 名，保证共享库下可重复执行
///        （uq_entity_type / uq_attr_catalog / uq_option_set 均为唯一约束）。
///   断言语义与文档完全一致。
/// </summary>
[Collection("Integration")]
public class EntityTypeTemplateServiceTests(EavApiFactory factory) : IDisposable
{
    private readonly IServiceScope _scope = factory.Services.CreateScope();

    public void Dispose() => _scope.Dispose();

    private TreeGraphDbContext CreateDb()
        => _scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();

    /// <summary>生成每次运行唯一的 EntityType 名（≤100 字符，满足 maxLength）。</summary>
    private static string NewType(string prefix)
        => $"TPL_{prefix}_{Guid.NewGuid():N}"[..24];

    private static async Task RegisterEntityTypeAsync(TreeGraphDbContext db, string entityType, string displayName)
    {
        db.EntityTypes.Add(new EntityTypeDefinition
        {
            EntityTypeId = Guid.NewGuid().ToString("D"),
            EntityType = entityType,
            DisplayName = displayName,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedAttrAsync(
        TreeGraphDbContext db, string entityType, string name, string dataType,
        bool required = false, JsonDocument? allowed = null)
    {
        db.AttributeCatalog.Add(new AttributeDefinition
        {
            AttributeId = Guid.NewGuid().ToString("D"),
            EntityType = entityType,
            AttributeName = name,
            DisplayName = name,

            DataType = dataType,
            IsRequired = required,
            IsDeleted = false,
            Version = 1,
            DisplayOrder = 0,
            AllowedValues = allowed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // ============================================================
    // 1. 基础：复制标量属性
    // ============================================================

    [Fact]
    public async Task Copy_ScalarAttributes_CopiesAll()
    {
        var db = CreateDb();
        var src = NewType("SRC");
        var tgt = NewType("TGT");
        await RegisterEntityTypeAsync(db, src, "源");
        await RegisterEntityTypeAsync(db, tgt, "目标");

        await SeedAttrAsync(db, src, "brand", "string", required: true);
        await SeedAttrAsync(db, src, "price", "decimal");
        await SeedAttrAsync(db, src, "inStock", "bool");

        var svc = new EntityTypeTemplateService(db);
        var result = await svc.CopyAttributesAsync(src, tgt);

        Assert.Equal(3, result.Copied);
        Assert.Equal(0, result.Skipped);

        var copied = await db.AttributeCatalog
            .Where(a => a.EntityType == tgt && !a.IsDeleted)
            .ToListAsync();
        Assert.Equal(3, copied.Count);
        Assert.Contains(copied, a => a.AttributeName == "brand" && a.IsRequired);
        Assert.Contains(copied, a => a.AttributeName == "price");
        Assert.Contains(copied, a => a.AttributeName == "inStock");
    }

    // ============================================================
    // 2. 跳过 composite / table
    // ============================================================

    [Fact]
    public async Task Copy_SkipsCompositeAndTable()
    {
        var db = CreateDb();
        var src = NewType("SRC");
        var tgt = NewType("TGT");
        await RegisterEntityTypeAsync(db, src, "源");
        await RegisterEntityTypeAsync(db, tgt, "目标");

        await SeedAttrAsync(db, src, "name", "string");
        await SeedAttrAsync(db, src, "address", "composite");
        await SeedAttrAsync(db, src, "items", "table");

        var svc = new EntityTypeTemplateService(db);
        var result = await svc.CopyAttributesAsync(src, tgt);

        Assert.Equal(1, result.Copied);
        Assert.Equal(2, result.Skipped);
        Assert.Contains(result.SkipReasons, r => r.Contains("composite"));
        Assert.Contains(result.SkipReasons, r => r.Contains("table"));
    }

    // ============================================================
    // 3. single_choice 深拷贝选项集
    // ============================================================

    [Fact]
    public async Task Copy_SingleChoice_ClonesOptionSet()
    {
        var db = CreateDb();
        var src = NewType("SRC");
        var tgt = NewType("TGT");
        await RegisterEntityTypeAsync(db, src, "源");
        await RegisterEntityTypeAsync(db, tgt, "目标");

        // 建选项集
        var setId = Guid.NewGuid().ToString("D");
        var set = new OptionSet
        {
            OptionSetId = setId,
            EntityType = src,
            SetName = "color",
            DisplayName = "颜色",
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        set.Items.Add(new OptionItem
        {
            OptionItemId = Guid.NewGuid().ToString("D"),
            OptionSetId = setId,
            Value = "red",
            Label = "红",
            DisplayOrder = 0,
            IsDefault = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        set.Items.Add(new OptionItem
        {
            OptionItemId = Guid.NewGuid().ToString("D"),
            OptionSetId = setId,
            Value = "blue",
            Label = "蓝",
            DisplayOrder = 1,
            IsDefault = false,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.OptionSets.Add(set);
        await db.SaveChangesAsync();

        // 属性引用该选项集
        db.AttributeCatalog.Add(new AttributeDefinition
        {
            AttributeId = Guid.NewGuid().ToString("D"),
            EntityType = src,
            AttributeName = "color",
            DisplayName = "颜色",
            DataType = "single_choice",
            RefOptionSetId = setId,
            IsDeleted = false,
            Version = 1,
            DisplayOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new EntityTypeTemplateService(db);
        var result = await svc.CopyAttributesAsync(src, tgt);

        Assert.Equal(1, result.Copied);

        var newAttr = await db.AttributeCatalog
            .FirstAsync(a => a.EntityType == tgt && a.AttributeName == "color");
        Assert.NotNull(newAttr.RefOptionSetId);
        Assert.NotEqual(setId, newAttr.RefOptionSetId);  // 应为新 ID

        var newSet = await db.OptionSets
            .Include(s => s.Items)
            .FirstAsync(s => s.OptionSetId == newAttr.RefOptionSetId);
        Assert.Equal(tgt, newSet.EntityType);
        Assert.Equal(2, newSet.Items.Count);
        Assert.Contains(newSet.Items, i => i.Value == "red" && i.IsDefault);
        Assert.Contains(newSet.Items, i => i.Value == "blue");
    }

    // ============================================================
    // 4. 幂等：目标已存在同名属性时跳过
    // ============================================================

    [Fact]
    public async Task Copy_Idempotent_SkipsExistingNames()
    {
        var db = CreateDb();
        var src = NewType("SRC");
        var tgt = NewType("TGT");
        await RegisterEntityTypeAsync(db, src, "源");
        await RegisterEntityTypeAsync(db, tgt, "目标");

        await SeedAttrAsync(db, src, "brand", "string");
        await SeedAttrAsync(db, tgt, "brand", "string");  // 目标已有

        var svc = new EntityTypeTemplateService(db);
        var result = await svc.CopyAttributesAsync(src, tgt);

        Assert.Equal(0, result.Copied);
        Assert.Equal(1, result.Skipped);
    }

    // ============================================================
    // 5. 源为空
    // ============================================================

    [Fact]
    public async Task Copy_EmptySource_ReturnsZero()
    {
        var db = CreateDb();
        var src = NewType("SRC");
        var tgt = NewType("TGT");
        await RegisterEntityTypeAsync(db, src, "源");
        await RegisterEntityTypeAsync(db, tgt, "目标");

        var svc = new EntityTypeTemplateService(db);
        var result = await svc.CopyAttributesAsync(src, tgt);

        Assert.Equal(0, result.Copied);
        Assert.Contains("源无属性可复制", result.SkipReasons);
    }

    // ============================================================
    // 6. 目标未注册 → 报错不复制
    // ============================================================

    [Fact]
    public async Task Copy_TargetNotRegistered_ReturnsZero()
    {
        var db = CreateDb();
        var src = NewType("SRC");
        var tgt = NewType("TGT");
        // tgt 未注册
        await RegisterEntityTypeAsync(db, src, "源");

        await SeedAttrAsync(db, src, "brand", "string");

        var svc = new EntityTypeTemplateService(db);
        var result = await svc.CopyAttributesAsync(src, tgt);

        Assert.Equal(0, result.Copied);
        Assert.Contains(result.SkipReasons, r => r.Contains("目标 EntityType 未注册"));
    }

    // ============================================================
    // 7. 源=目标 → 跳过
    // ============================================================

    [Fact]
    public async Task Copy_SameSourceAndTarget_Skips()
    {
        var db = CreateDb();
        var svc = new EntityTypeTemplateService(db);

        var result = await svc.CopyAttributesAsync("X", "X");
        Assert.Equal(0, result.Copied);
        Assert.Contains("源与目标相同", result.SkipReasons[0]);
    }
}
