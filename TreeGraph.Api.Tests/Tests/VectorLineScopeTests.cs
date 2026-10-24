using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.SemanticSearch;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.NodeSky.Entities;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 线路向量对账范围（VectorSynchronizer.ResolveLineScopeAsync）单元测试：
/// EF Core InMemory，无需 Docker / Qdrant / vLLM。
/// R7 契约：范围 = 线路自身 + nodes（host_type='LineNode' 且 line_id = lineId）；
/// 线路不存在 → 空集合。
/// 嵌入与向量存储依赖以哑实例构造——范围解析只查 PG，测试中不会触达它们。
/// </summary>
public class VectorLineScopeTests
{
    private const string LineId = "aaaaaaaa-0000-0000-0000-000000000001";

    /// <summary>
    /// 测试专用 DbContext：仅映射 Line + Node（范围解析仅依赖这两张表）。
    /// </summary>
    private sealed class TestDbContext(DbContextOptions<TreeGraphDbContext> options)
        : TreeGraphDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            foreach (var entityType in mb.Model.GetEntityTypes()
                         .Where(t => t.ClrType != typeof(Line) && t.ClrType != typeof(Node))
                         .ToList())
            {
                mb.Ignore(entityType.ClrType);
            }
            mb.Entity<Line>(e => e.HasKey(x => x.Id));
            mb.Entity<Node>(e => e.HasKey(x => x.Id));
        }
    }

    private static VectorSynchronizer CreateSynchronizer(TreeGraphDbContext db)
    {
        var options = Options.Create(new SemanticSearchOptions());
        var embedding = new EmbeddingClient(
            new HttpClient(), options, NullLogger<EmbeddingClient>.Instance);
        var store = new QdrantVectorStore(
            new QdrantClient("localhost"), options, NullLogger<QdrantVectorStore>.Instance);
        return new VectorSynchronizer(
            db, new SourceTextBuilder(db), embedding, store,
            NullLogger<VectorSynchronizer>.Instance);
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

    private static Node LineNode(string id, string lineId, string name)
        => new() { Id = id, HostType = "LineNode", LineId = lineId, Name = name };

    [Fact]
    public async Task 线路存在且有节点_范围含线路自身与全部节点()
    {
        await using var db = CreateDb();
        db.Lines.Add(new Line { Id = LineId, Name = "10kV 一线" });
        db.Nodes.Add(LineNode("n-1", LineId, "杆塔 #001"));
        db.Nodes.Add(LineNode("n-2", LineId, "杆塔 #002"));
        await db.SaveChangesAsync();

        var scope = await CreateSynchronizer(db).ResolveLineScopeAsync(LineId, default);

        Assert.Equal(3, scope.Count);
        Assert.Contains(LineId, scope);
        Assert.Contains("n-1", scope);
        Assert.Contains("n-2", scope);
    }

    [Fact]
    public async Task 范围不含其他线路的节点()
    {
        await using var db = CreateDb();
        db.Lines.Add(new Line { Id = LineId, Name = "10kV 一线" });
        db.Lines.Add(new Line { Id = "line-b", Name = "10kV 二线" });
        db.Nodes.Add(LineNode("n-1", LineId, "杆塔 #001"));
        db.Nodes.Add(LineNode("n-x", "line-b", "杆塔 #X"));
        await db.SaveChangesAsync();

        var scope = await CreateSynchronizer(db).ResolveLineScopeAsync(LineId, default);

        Assert.Equal(2, scope.Count);
        Assert.DoesNotContain("n-x", scope);
        Assert.DoesNotContain("line-b", scope);
    }

    [Fact]
    public async Task 线路存在但无节点_范围仅线路自身()
    {
        await using var db = CreateDb();
        db.Lines.Add(new Line { Id = LineId, Name = "空线路" });
        await db.SaveChangesAsync();

        var scope = await CreateSynchronizer(db).ResolveLineScopeAsync(LineId, default);

        Assert.Single(scope);
        Assert.Contains(LineId, scope);
    }

    [Fact]
    public async Task 线路不存在_范围为空()
    {
        await using var db = CreateDb();
        // 只有其他线路的数据
        db.Lines.Add(new Line { Id = "line-b", Name = "10kV 二线" });
        db.Nodes.Add(LineNode("n-x", "line-b", "杆塔 #X"));
        await db.SaveChangesAsync();

        var scope = await CreateSynchronizer(db).ResolveLineScopeAsync(LineId, default);

        Assert.Empty(scope);
    }
}
