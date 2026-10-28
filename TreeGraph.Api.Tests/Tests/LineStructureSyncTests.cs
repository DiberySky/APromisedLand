using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.GraphSyncSky;
using TreeGraph.Shared.NodeSky.Entities;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// R7 线路入图：Line→LineNode 归属走 parent_of；line_segment 段边直写 edges 表
/// （EdgeSky 顶级边，rank 恒 0），经语义边段统一装载。
/// 使用 InMemory 库 + 伪 NebulaGraph REST，不依赖图数据库。
/// </summary>
public class LineStructureSyncTests
{
    // ============================================================
    // SyncInodeAsync：parent_of（Line → LineNode）
    // ============================================================

    [Fact]
    public async Task SyncInode_LineNode_UpsertsParentOfEdgeToLine()
    {
        var handler = new FakeNebulaHandler();
        await using var db = CreateDb(Guid.NewGuid().ToString());
        SeedLine(db, "line-1", "n1");
        var sync = CreateSynchronizer(db, handler);

        var written = await sync.SyncInodeAsync("n1");

        Assert.True(written);
        var edge = Assert.Single(RecordedEdges(handler),
            e => e.Type == GraphNaming.ParentOfEdge);
        Assert.Equal("line-1", edge.Src);
        Assert.Equal("n1", edge.Dst);
    }

    [Fact]
    public async Task SyncInode_LineNode_WithoutLineId_NotWritten()
    {
        var handler = new FakeNebulaHandler();
        await using var db = CreateDb(Guid.NewGuid().ToString());
        db.Nodes.Add(new Node { Id = "orphan", HostType = "LineNode", Name = "游离" });
        await db.SaveChangesAsync();
        var sync = CreateSynchronizer(db, handler);

        var written = await sync.SyncInodeAsync("orphan");

        Assert.False(written); // line_id 为 null → 顶点链路拒绝
    }

    [Fact]
    public async Task SyncInode_Line_WritesVertexWithoutParentEdge()
    {
        var handler = new FakeNebulaHandler();
        await using var db = CreateDb(Guid.NewGuid().ToString());
        SeedLine(db, "line-1");
        var sync = CreateSynchronizer(db, handler);

        var written = await sync.SyncInodeAsync("line-1");

        Assert.True(written);
        Assert.DoesNotContain(RecordedEdges(handler),
            e => e.Type == GraphNaming.ParentOfEdge);
    }

    // ============================================================
    // 按类别同步：bulk 含 parent_of，且任何请求都不再出现 line_contains
    // ============================================================

    [Fact]
    public async Task SyncCategoryLine_BulkIncludesParentOfEdges_NoLineContains()
    {
        var handler = new FakeNebulaHandler();
        await using var db = CreateDb(Guid.NewGuid().ToString());
        SeedLine(db, "line-1", "n1", "n2");

        await CreateSynchronizer(db, handler)
            .SyncCategoryAsync(SyncCategory.Line, CancellationToken.None);

        var bulk = handler.Requests.Single(r => r.Path.EndsWith("/graph/bulk"));
        using var doc = JsonDocument.Parse(bulk.Body!);
        var parentEdges = doc.RootElement.GetProperty("edges").EnumerateArray()
            .Where(e => e.GetProperty("edge_type").GetString() == GraphNaming.ParentOfEdge)
            .ToList();
        Assert.Equal(2, parentEdges.Count);
        Assert.All(parentEdges, e => Assert.Equal("line-1", e.GetProperty("src_vid").GetString()));
        Assert.Contains(parentEdges, e => e.GetProperty("dst_vid").GetString() == "n1");
        Assert.Contains(parentEdges, e => e.GetProperty("dst_vid").GetString() == "n2");

        // line_contains 已整体下线
        Assert.DoesNotContain(handler.Requests,
            r => r.Body?.Contains("line_contains", StringComparison.Ordinal) == true);
    }

    // ============================================================
    // 全量重建：line_segment 段边存 edges 表，计入 SemanticEdges，边类型 = line_segment
    // ============================================================

    [Fact]
    public async Task RebuildAll_LineSegmentEdge_CountsSemanticEdges()
    {
        var dbName = Guid.NewGuid().ToString();
        await using (var seed = CreateDb(dbName))
        {
            SeedLine(seed, "line-1", "n1", "n2");
            seed.Edges.Add(new Edge
            {
                SpaceId = TestSpaceId,
                EdgeType = GraphNaming.LineSegmentEdge,
                SrcVid = "n1",
                DstVid = "n2",
                Rank = 0,
                Props = JsonSerializer.SerializeToElement(
                    new Dictionary<string, string> { ["name"] = "第一段" })
            });
            await seed.SaveChangesAsync();
        }

        var handler = new FakeNebulaHandler();
        var initializer = await CreateReadyInitializerAsync(dbName, handler);
        var options = Options.Create(new GraphSyncOptions());
        await using var db = CreateDb(dbName);
        var sync = new GraphSynchronizer(db, CreateClient(handler),
            initializer, options, NullLogger<GraphSynchronizer>.Instance);

        var result = await sync.RebuildAllAsync(CancellationToken.None);

        Assert.Equal(1, result.SemanticEdges);
        Assert.Equal(2, result.LineEdges); // parent_of ×2（Line→LineNode）
        var edge = FindBulkEdge(handler, GraphNaming.LineSegmentEdge);
        Assert.Equal("n1", edge.GetProperty("src_vid").GetString());
        Assert.Equal("n2", edge.GetProperty("dst_vid").GetString());
        Assert.Equal(0, edge.GetProperty("rank").GetInt64());
        Assert.Equal("第一段", edge.GetProperty("properties").GetProperty("name").GetString());
    }

    // ============================================================
    // schema 初始化：语义边类型以 PG edge_types/edge_type_props 为准（line_segment 为内置 seed）
    // ============================================================

    [Fact]
    public async Task Schema_CreatesEdgeTypeForLineSegmentSeed()
    {
        var dbName = Guid.NewGuid().ToString();
        var handler = new FakeNebulaHandler();
        await using (var seed = CreateDb(dbName))
        {
            // 物化内置种子（EdgeType line_segment + name STRING NULL prop）：改由启动时幂等播种器写入；
            // 图空间化后播种与 schema 初始化均按 spaces 行循环，先补一行空间
            seed.Database.EnsureCreated();
            seed.Spaces.Add(new Api.SpaceSky.Entities.Space
            {
                Id = TestSpaceId,
                Name = "测试空间",
                Type = Api.SpaceSky.Entities.SpaceType.Dynamic
            });
            await seed.SaveChangesAsync();
            await TreeGraph.Api.Data.Seeding.BuiltinEdgeTypeSeeder.SeedAsync(seed);
        }

        await CreateReadyInitializerAsync(dbName, handler);

        // 每个 Dynamic 空间各建一次（HasData 种子仅「武汉供电段」为 Dynamic + 测试空间 = 2）
        var creates = handler.Queries
            .Where(q => q.Contains($"CREATE EDGE IF NOT EXISTS `{GraphNaming.LineSegmentEdge}`"))
            .ToList();
        Assert.Equal(2, creates.Count);
        Assert.All(creates, c => Assert.Contains("`name` STRING NULL", c));

        // line_contains 不再建边
        Assert.DoesNotContain(handler.QueryTexts,
            q => q.Contains("line_contains", StringComparison.Ordinal));
    }

    // ============================================================
    // helpers
    // ============================================================

    private const string TestSpaceId = "space-test";

    private static void SeedLine(TreeGraphDbContext db, string lineId, params string[] nodeIds)
    {
        // 图空间化：线路/段边归属空间，spaces 行 + lines 行（SpaceId）必须齐备
        if (!db.Spaces.Any(s => s.Id == TestSpaceId))
            db.Spaces.Add(new Api.SpaceSky.Entities.Space
            {
                Id = TestSpaceId,
                Name = "测试空间",
                Type = Api.SpaceSky.Entities.SpaceType.Dynamic
            });
        db.Nodes.Add(new Node { Id = lineId, HostType = "Line", Name = "测试线路" });
        db.Lines.Add(new TreeGraph.Shared.LineSky.Entities.Line
        {
            Id = lineId,
            Name = "测试线路",
            SpaceId = TestSpaceId
        });
        for (var i = 0; i < nodeIds.Length; i++)
        {
            db.Nodes.Add(new Node
            {
                Id = nodeIds[i],
                HostType = "LineNode",
                Name = $"节点{i + 1}",
                LineId = lineId
            });
        }
        db.SaveChanges();
    }

    private static TestDbContext CreateDb(string dbName)
        => new(new DbContextOptionsBuilder<TreeGraphDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    private static NebulaGraphClient CreateClient(FakeNebulaHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://nebula.test/") };
        return new NebulaGraphClient(http, NullLogger<NebulaGraphClient>.Instance);
    }

    private static GraphSynchronizer CreateSynchronizer(
        TreeGraphDbContext db, FakeNebulaHandler handler)
    {
        var options = Options.Create(new GraphSyncOptions());
        return new GraphSynchronizer(db, CreateClient(handler),
            null!, options, NullLogger<GraphSynchronizer>.Instance);
    }

    private static async Task<GraphSchemaInitializer> CreateReadyInitializerAsync(
        string dbName, FakeNebulaHandler handler)
    {
        var services = new ServiceCollection();
        services.AddScoped<TreeGraphDbContext>(_ => CreateDb(dbName));
        // 生命周期覆盖整个测试：initializer 持有该 provider 的 scope factory
        var provider = services.BuildServiceProvider();

        var initializer = new GraphSchemaInitializer(
            CreateClient(handler),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<GraphSchemaInitializer>.Instance);

        await initializer.StartAsync(CancellationToken.None);
        await initializer.EnsureReadyAsync().WaitAsync(TimeSpan.FromSeconds(30));
        return initializer;
    }

    private sealed record RecordedEdge(string Type, string Src, string Dst);

    private static List<RecordedEdge> RecordedEdges(FakeNebulaHandler handler)
        => handler.Requests
            .Where(r => r.Method == "POST" && r.Path.EndsWith("/graph/edges"))
            .Select(r =>
            {
                using var doc = JsonDocument.Parse(r.Body!);
                var root = doc.RootElement;
                return new RecordedEdge(
                    root.GetProperty("edge_type").GetString()!,
                    root.GetProperty("src_vid").GetString()!,
                    root.GetProperty("dst_vid").GetString()!);
            })
            .ToList();

    private static JsonElement FindBulkEdge(FakeNebulaHandler handler, string edgeType)
    {
        var bulk = handler.Requests.Single(r => r.Path.EndsWith("/graph/bulk"));
        using var doc = JsonDocument.Parse(bulk.Body!);
        return doc.RootElement.GetProperty("edges").EnumerateArray()
            .Single(e => e.GetProperty("edge_type").GetString() == edgeType)
            .Clone();
    }

    private sealed record RecordedRequest(string Method, string Path, string? Body);

    /// <summary>InMemory 测试上下文：忽略 InMemory 不支持的 JsonDocument 属性。</summary>
    private sealed class TestDbContext(DbContextOptions<TreeGraphDbContext> options)
        : TreeGraphDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            foreach (var et in mb.Model.GetEntityTypes().ToList())
            {
                foreach (var prop in et.GetProperties()
                             .Where(p => p.ClrType == typeof(JsonDocument))
                             .ToList())
                {
                    mb.Entity(et.ClrType).Ignore(prop.Name);
                }
            }
        }
    }

    /// <summary>
    /// 伪 NebulaGraph REST：写入返回 success；DESCRIBE 按既有列返回（全部齐备，不做 ALTER）。
    /// </summary>
    private sealed class FakeNebulaHandler : HttpMessageHandler
    {
        private readonly List<RecordedRequest> _requests = new();

        public IReadOnlyList<RecordedRequest> Requests
        {
            get { lock (_requests) return _requests.ToList(); }
        }

        /// <summary>nGQL 原文列表（请求体 query 字段）。</summary>
        public List<string> QueryTexts
        {
            get
            {
                lock (_requests)
                {
                    return _requests
                        .Where(r => r.Path.EndsWith("/graph/query"))
                        .Select(r => ExtractQuery(r.Body) ?? "")
                        .ToList();
                }
            }
        }

        public List<string> Queries => QueryTexts;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(ct);
            lock (_requests)
                _requests.Add(new RecordedRequest(
                    request.Method.Method, request.RequestUri!.AbsolutePath, body));

            if (request.RequestUri.AbsolutePath.EndsWith("/graph/query"))
            {
                var ngql = ExtractQuery(body);
                if (ngql is not null
                    && ngql.StartsWith("DESCRIBE EDGE", StringComparison.Ordinal))
                {
                    var rows = new[] { "value_id", "attribute_id", "name" }
                        .Select(f => (object)new { Field = f }).ToList();
                    return Json(new { success = true, data = rows, row_count = rows.Count });
                }

                if (ngql is not null && ngql.StartsWith("DESCRIBE TAG", StringComparison.Ordinal))
                {
                    var tagName = ngql.Split('`').Skip(1).FirstOrDefault() ?? "";
                    var cols = GraphNaming.TagColumns.TryGetValue(tagName, out var known)
                        ? known
                        : [];
                    var rows = cols.Select(f => (object)new { Field = f }).ToList();
                    return Json(new { success = true, data = rows, row_count = rows.Count });
                }

                // GO 查询等：返回空行集
                return Json(new
                {
                    success = true,
                    data = Array.Empty<object>(),
                    row_count = 0
                });
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/graph/bulk"))
            {
                return Json(new
                {
                    vertices_total = 0,
                    vertices_success = 0,
                    edges_total = 0,
                    edges_success = 0
                });
            }

            return Json(new { success = true });
        }

        private static HttpResponseMessage Json(object payload) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        private static string? ExtractQuery(string? body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.TryGetProperty("query", out var q)
                    ? q.GetString()
                    : body;
            }
            catch (JsonException)
            {
                return body;
            }
        }
    }
}
