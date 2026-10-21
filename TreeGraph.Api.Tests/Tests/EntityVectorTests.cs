using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.SemanticSearch;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 实体向量文本（SourceTextBuilder.BuildEntityTextAsync，v3 path_view 结构）单元测试：
/// EF Core InMemory，无需 Docker / Qdrant / vLLM。
/// 契约：
///   有属性值 → 文本 = [宿主 path_view 前缀] + 类型行 + 属性行；
///   无任何属性值/表行 → null（上层据此删点）。
/// </summary>
public class EntityVectorTests
{
    private const string EntityType = "equipment";
    private const string EntityId = "eeeeeeee-0000-0000-0000-000000000001";

    /// <summary>
    /// 测试专用 DbContext：仅映射实体文本构建依赖的三张表
    /// （AttributeCatalog / AttributeValues / CustomTableRows），导航与 JsonDocument 列忽略。
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
                e.Ignore(x => x.RowData);
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
            ValueId = "v-1", EntityId = EntityId, EntityType = EntityType,
            AttributeId = "attr-1", ValueString = "SF6-110"
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task 有属性值_文本含类型行与属性行()
    {
        await using var db = await CreateDbWithValueAsync();

        var result = await new SourceTextBuilder(db)
            .BuildEntityTextAsync(EntityType, EntityId);

        Assert.NotNull(result);
        Assert.Contains($"类型：{EntityType}", result.Text);
        Assert.Contains("型号：SF6-110", result.Text);
    }

    [Fact]
    public async Task 有宿主路径_文本以path_view前缀开头()
    {
        await using var db = await CreateDbWithValueAsync();
        const string hostNodeText = "层级路径：华东 > 变电站A\n当前节点：1号主变";

        var result = await new SourceTextBuilder(db)
            .BuildEntityTextAsync(EntityType, EntityId, hostNodeText);

        Assert.NotNull(result);
        Assert.StartsWith(hostNodeText, result.Text);
        Assert.Contains($"类型：{EntityType}", result.Text);
    }

    [Fact]
    public async Task 无属性值_返回null()
    {
        await using var db = await CreateDbWithValueAsync();

        var result = await new SourceTextBuilder(db)
            .BuildEntityTextAsync(EntityType, "eeeeeeee-9999-9999-9999-999999999999");

        Assert.Null(result);
    }
}
