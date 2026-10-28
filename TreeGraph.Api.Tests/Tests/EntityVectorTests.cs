using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.SemanticSearch;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 实体/行向量文本（SourceTextBuilder.BuildEntityTextAsync / BuildRowTextAsync）单元测试：
/// EF Core InMemory，无需 Docker / Qdrant / vLLM。
/// 契约（v4）：
///   实体文本 = [宿主 path_view 前缀] + 类型行 + 属性行（不再内嵌自定义表行）；
///   行文本   = [宿主 path_view 前缀] + 类型行 + 表格行 + 行内容；
///   全部键值行统一 name = value（等号分隔）；
///   实体无属性值 → null（实体点删除，行点独立维护）；行无内容 → null。
/// </summary>
public class EntityVectorTests
{
    private const string EntityType = "equipment";
    private const string EntityId = "eeeeeeee-0000-0000-0000-000000000001";

    /// <summary>
    /// 测试专用 DbContext：仅映射文本构建依赖的三张表
    /// （AttributeCatalog / AttributeValues / CustomTableRows），
    /// 导航忽略；RowData 用 JsonDocument ↔ string 转换映射。
    /// </summary>
    private sealed class TestDbContext(DbContextOptions<TreeGraphDbContext> options)
        : TreeGraphDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            foreach (var entityType in mb.Model.GetEntityTypes()
                         .Where(t => t.ClrType != typeof(AttributeDefinition)
                                     && t.ClrType != typeof(AttributeValue)
                                     && t.ClrType != typeof(CustomTableRow))
                         .ToList())
            {
                mb.Ignore(entityType.ClrType);
            }

            mb.Entity<AttributeDefinition>(e =>
            {
                e.HasKey(x => x.AttributeId);
                e.Ignore(x => x.RefCompositeType);
                e.Ignore(x => x.RefTableDefinition);
                e.Ignore(x => x.RefOptionSet);
                e.Ignore(x => x.Unit);
                e.Ignore(x => x.AllowedValues);
                e.Ignore(x => x.ValidationRule);
            });
            mb.Entity<AttributeValue>(e =>
            {
                e.HasKey(x => x.ValueId);
                e.Ignore(x => x.Unit);
                e.Ignore(x => x.Attribute);
                e.Ignore(x => x.ValueFileMeta);
                e.Ignore(x => x.ValueJsonb);
            });
            mb.Entity<CustomTableRow>(e =>
            {
                e.HasKey(x => x.RowId);
                e.Ignore(x => x.Attribute);
                e.Ignore(x => x.Table);
                e.Property(x => x.RowData).HasConversion(
                    d => d.RootElement.GetRawText(),
                    s => System.Text.Json.JsonDocument.Parse(s, default));
            });
        }
    }

    private static TreeGraphDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TreeGraphDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TestDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<TreeGraphDbContext> CreateDbWithValueAsync()
    {
        var db = CreateDb();
        db.AttributeCatalog.Add(new AttributeDefinition
        {
            AttributeId = "attr-1", EntityType = EntityType,
            AttributeName = "model", DisplayName = "型号", DisplayOrder = 1
        });
        db.AttributeValues.Add(new AttributeValue
        {
            ValueId = "v-1", EntityId = EntityId,
            AttributeId = "attr-1", ValueString = "SF6-110"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static CustomTableRow MakeRow(
        string rowId, string attributeId, string? entityId = null) => new()
    {
        RowId = rowId,
        AttributeId = attributeId,
        ParentEntityType = EntityType,
        ParentEntityId = entityId ?? EntityId,
        RowOrder = 0,
        RowData = System.Text.Json.JsonDocument.Parse(
            """{"缺陷类型":"绝缘子破损","等级":"严重"}"""),
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now
    };

    [Fact]
    public async Task 有属性值_文本含类型行与属性行()
    {
        await using var db = await CreateDbWithValueAsync();

        var result = await new SourceTextBuilder(db)
            .BuildEntityTextAsync(EntityType, EntityId);

        Assert.NotNull(result);
        Assert.Contains($"类型 = {EntityType}", result.Text);
        Assert.Contains("型号 = SF6-110", result.Text);
    }

    [Fact]
    public async Task 实体文本不再内嵌自定义表行()
    {
        await using var db = await CreateDbWithValueAsync();
        db.AttributeCatalog.Add(new AttributeDefinition
        {
            AttributeId = "tbl-defect", EntityType = EntityType,
            AttributeName = "defects", DisplayName = "缺陷记录", DisplayOrder = 2
        });
        db.CustomTableRows.Add(MakeRow("row-1", "tbl-defect"));
        await db.SaveChangesAsync();

        var result = await new SourceTextBuilder(db)
            .BuildEntityTextAsync(EntityType, EntityId);

        Assert.NotNull(result);
        Assert.DoesNotContain("行 = ", result.Text);
        Assert.DoesNotContain("缺陷类型", result.Text);
    }

    [Fact]
    public async Task 有宿主路径_文本以path_view前缀开头()
    {
        await using var db = await CreateDbWithValueAsync();
        const string hostNodeText = "层级路径 = 华东 > 变电站A\n当前节点 = 1号主变";

        var result = await new SourceTextBuilder(db)
            .BuildEntityTextAsync(EntityType, EntityId, hostNodeText);

        Assert.NotNull(result);
        Assert.StartsWith(hostNodeText, result.Text);
        Assert.Contains($"类型 = {EntityType}", result.Text);
    }

    [Fact]
    public async Task 无属性值_返回null()
    {
        await using var db = await CreateDbWithValueAsync();

        var result = await new SourceTextBuilder(db)
            .BuildEntityTextAsync(EntityType, "eeeeeeee-9999-9999-9999-999999999999");

        Assert.Null(result);
    }

    [Fact]
    public async Task 行文本含宿主前缀表格名与行内容()
    {
        await using var db = await CreateDbWithValueAsync();
        db.AttributeCatalog.Add(new AttributeDefinition
        {
            AttributeId = "tbl-defect", EntityType = EntityType,
            AttributeName = "defects", DisplayName = "缺陷记录", DisplayOrder = 2
        });
        await db.SaveChangesAsync();
        const string hostNodeText = "层级路径 = 华东 > 变电站A\n当前节点 = 1号主变";
        var row = MakeRow("row-1", "tbl-defect");

        var result = await new SourceTextBuilder(db)
            .BuildRowTextAsync(row, hostNodeText);

        Assert.NotNull(result);
        Assert.StartsWith(hostNodeText, result.Text);
        Assert.Contains($"类型 = {EntityType}", result.Text);
        Assert.Contains("表格 = 缺陷记录", result.Text);
        Assert.Contains("行 = 缺陷类型=绝缘子破损，等级=严重", result.Text);
    }

    [Fact]
    public async Task 行文本无宿主_从类型行开始()
    {
        await using var db = await CreateDbWithValueAsync();
        var row = MakeRow("row-1", "tbl-unknown");

        var result = await new SourceTextBuilder(db)
            .BuildRowTextAsync(row, null);

        Assert.NotNull(result);
        Assert.StartsWith($"类型 = {EntityType}", result.Text);
        // 属性定义缺失时表格名回退为 AttributeId
        Assert.Contains("表格 = tbl-unknown", result.Text);
    }

    [Fact]
    public async Task 行无内容_返回null()
    {
        await using var db = await CreateDbWithValueAsync();
        var row = new CustomTableRow
        {
            RowId = "row-empty", AttributeId = "tbl-x",
            ParentEntityType = EntityType, ParentEntityId = EntityId,
            RowOrder = 0,
            RowData = System.Text.Json.JsonDocument.Parse("""{"备注":""}"""),
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };

        var result = await new SourceTextBuilder(db)
            .BuildRowTextAsync(row, null);

        Assert.Null(result);
    }
}
