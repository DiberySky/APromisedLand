using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Api.GraphSyncSky;
using TreeGraph.Api.LineSky.Entities;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.NodeSky.Entities;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// R5 线路入图：GraphSynchronizer.SyncLineStructureAsync 的结构边（line_segment）投影测试。
/// 使用 InMemory 库 + 伪 NebulaGraph REST（记录请求、按 nGQL 返回既有边），不依赖图数据库。
/// </summary>
public class LineStructureSyncTests
{
    // ============================================================
    // SyncLineStructureAsync
    // ============================================================

    [Fact]
    public async Task SyncLineStructure_UpsertsSegmentEdges()
    {
        var lineId = "line-1";
        await using var db = CreateDb(
            Guid.NewGuid().ToString(), lineId, ["n1", "n2", "n3"],
            [("n1", "n2", 2.5m, "第一段"), ("n2", "n3", null, null)]);

        var handler = new FakeNebulaHandler();
        await CreateSynchronizer(db, handler).SyncLineStructureAsync(lineId);

        var segmentEdges = RecordedEdges(handler)
            .Where(e => e.Type == GraphNaming.LineSegmentEdge)
            .ToList();

        Assert.Equal(2, segmentEdges.Count);
        var first = Assert.Single(segmentEdges, e => e.Src == "n1" && e.Dst == "n2");
        Assert.Equal(2.5, first.Props!["weight"].GetDouble(), 3);
        Assert.Equal("第一段", first.Props["display_name"].GetString());
        Assert.Equal(lineId, first.Props["line_id"].GetString());
        Assert.False(string.IsNullOrEmpty(first.Props["edge_id"].GetString()));

        var second = Assert.Single(segmentEdges, e => e.Src == "n2" && e.Dst == "n3");
        Assert.False(second.Props!.ContainsKey("weight")); // null 属性不下发
        Assert.False(second.Props.ContainsKey("display_name"));
    }

    [Fact]
    public async Task SyncLineStructure_DeletesStaleSegmentEdges()
    {
        var lineId = "line-2";
        await using var db = CreateDb(
            Guid.NewGuid().ToString(), lineId, ["n1", "n2", "n3"],
            [("n2", "n3", null, null)]);

        var handler = new FakeNebulaHandler([("n1", "n2")]);
        await CreateSynchronizer(db, handler).SyncLineStructureAsync(lineId);

        var deleted = RecordedEdgeDeletes(handler);
        var stale = Assert.Single(deleted, e => e.Src == "n1" && e.Dst == "n2");
        Assert.Equal(GraphNaming.LineSegmentEdge, stale.Type);

        var upserts = RecordedEdges(handler)
            .Where(e => e.Type == GraphNaming.LineSegmentEdge)
            .ToList();
        Assert.Single(upserts, e => e.Src == "n2" && e.Dst == "n3");
    }

    [Fact]
    public async Task SyncLineStructure_ClearedStructure_DeletesAllSegmentEdges()
    {
        var lineId = "line-3";
        await using var db = CreateDb(
            Guid.NewGuid().ToString(), lineId, ["n1", "n2"], []);

        var handler = new FakeNebulaHandler([("n1", "n2")]);
        await CreateSynchronizer(db, handler).SyncLineStructureAsync(lineId);

        var deleted = RecordedEdgeDeletes(handler);
        Assert.Single(deleted, e => e.Type == GraphNaming.LineSegmentEdge
                                    && e.Src == "n1" && e.Dst == "n2");
        Assert.DoesNotContain(
            RecordedEdges(handler), e => e.Type == GraphNaming.LineSegmentEdge);
    }

    [Fact]
    public async Task SyncLineStructure_LineWithoutNodes_NoGraphWrites()
    {
        await using var db = CreateDb(
            Guid.NewGuid().ToString(), "line-4", [], []);

        var handler = new FakeNebulaHandler([("x", "y")]);
        await CreateSynchronizer(db, handler).SyncLineStructureAsync("line-4");

        Assert.Empty(handler.Requests);
    }

    // ============================================================
    // 全量重建 / 按类别同步（共用 AddLineGraphAsync）
    // ============================================================

    [Fact]
    public async Task SyncCategoryLine_BulkIncludesSegmentEdges()
    {
        var lineId = "line-5";
        await using var db = CreateDb(
            Guid.NewGuid().ToString(), lineId, ["n1", "n2"],
            [("n1", "n2", 1.25m, "重建段")]);

        var handler = new FakeNebulaHandler();
        await CreateSynchronizer(db, handler)
            .SyncCategoryAsync(SyncCategory.Line, CancellationToken.None);

        var bulk = handler.Requests.Single(r => r.Path.EndsWith("/graph/bulk"));
        using var doc = JsonDocument.Parse(bulk.Body!);
        var edges = doc.RootElement.GetProperty("edges").EnumerateArray()
            .Where(e => e.GetProperty("edge_type").GetString() == GraphNaming.LineSegmentEdge)
            .ToList();

        var segment = Assert.Single(edges);
        Assert.Equal("n1", segment.GetProperty("src_vid").GetString());
        Assert.Equal("n2", segment.GetProperty("dst_vid").GetString());

        var props = segment.GetProperty("properties");
        Assert.Equal(1.25, props.GetProperty("weight").GetDouble(), 3);
        Assert.Equal("重建段", props.GetProperty("display_name").GetString());
        Assert.Equal(lineId, props.GetProperty("line_id").GetString());
    }

    // ============================================================
    // helpers
    // ============================================================

    private static TreeGraphDbContext CreateDb(
        string dbName,
        string lineId,
        string[] nodeIds,
        (string From, string To, decimal? Weight, string? DisplayName)[] segments)
    {
        var options = new DbContextOptionsBuilder<TreeGraphDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new TestDbContext(options);

        db.Lines.Add(new Line { Id = lineId, Name = "测试线路" });
        for (var i = 0; i < nodeIds.Length; i++)
        {
            db.Nodes.Add(new Node { Id = nodeIds[i], HostType = "LineNode", Name = $"节点{i + 1}" });
            db.LineNodes.Add(new LineNode { Id = nodeIds[i], LineId = lineId });
        }

        foreach (var s in segments)
        {
            db.LineStructureSegments.Add(new LineStructureSegment
            {
                LineId = lineId,
                EdgeId = Guid.NewGuid().ToString("N"),
                FromNodeId = s.From,
                ToNodeId = s.To,
                Weight = s.Weight,
                DisplayName = s.DisplayName
            });
        }

        db.SaveChanges();
        return db;
    }

    private static GraphSynchronizer CreateSynchronizer(
        TreeGraphDbContext db, FakeNebulaHandler handler)
    {
        var options = Options.Create(new GraphSyncOptions { Space = "test" });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://nebula.test/") };
        var client = new NebulaGraphClient(http, options, NullLogger<NebulaGraphClient>.Instance);

        // schemaInitializer 仅 RebuildAllAsync 使用；本测试走 SyncLineStructure/SyncCategory
        return new GraphSynchronizer(
            db, client, null!, options, NullLogger<GraphSynchronizer>.Instance);
    }

    private sealed record RecordedEdge(
        string Type, string Src, string Dst, Dictionary<string, JsonElement>? Props);

    private static List<RecordedEdge> RecordedEdges(FakeNebulaHandler handler)
        => handler.Requests
            .Where(r => r.Method == "POST" && r.Path.EndsWith("/graph/edges"))
            .Select(r =>
            {
                using var doc = JsonDocument.Parse(r.Body!);
                var root = doc.RootElement;
                var props = root.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object
                    ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
                    : null;
                return new RecordedEdge(
                    root.GetProperty("edge_type").GetString()!,
                    root.GetProperty("src_vid").GetString()!,
                    root.GetProperty("dst_vid").GetString()!,
                    props);
            })
            .ToList();

    private static List<RecordedEdge> RecordedEdgeDeletes(FakeNebulaHandler handler)
        => handler.Requests
            .Where(r => r.Method == "DELETE" && r.Path.EndsWith("/graph/edges"))
            .Select(r =>
            {
                using var doc = JsonDocument.Parse(r.Body!);
                var root = doc.RootElement;
                return new RecordedEdge(
                    root.GetProperty("edge_type").GetString()!,
                    root.GetProperty("src_vid").GetString()!,
                    root.GetProperty("dst_vid").GetString()!,
                    null);
            })
            .ToList();

    private sealed record RecordedRequest(string Method, string Path, string? Body);

    /// <summary>
    /// InMemory 测试上下文：先让基类完成全部配置，再忽略 InMemory 不支持的 JsonDocument 属性。
    /// </summary>
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
    /// 伪 NebulaGraph REST：所有写入返回 success；GO 查询按预置结构边返回 src/dst 行。
    /// </summary>
    private sealed class FakeNebulaHandler : HttpMessageHandler
    {
        private readonly HashSet<(string From, string To)> _segmentEdges;
        private readonly List<RecordedRequest> _requests = new();

        public FakeNebulaHandler(IEnumerable<(string, string)>? existingSegments = null)
            => _segmentEdges = existingSegments?.ToHashSet() ?? new HashSet<(string, string)>();

        public IReadOnlyList<RecordedRequest> Requests
        {
            get { lock (_requests) return _requests.ToList(); }
        }

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
                var rows = new List<object>();
                var ngql = ExtractQuery(body);
                if (ngql?.Contains(GraphNaming.LineSegmentEdge, StringComparison.Ordinal) == true)
                {
                    var match = Regex.Match(ngql, "GO FROM \"([^\"]+)\"");
                    if (match.Success)
                    {
                        var vid = match.Groups[1].Value;
                        rows.AddRange(_segmentEdges
                            .Where(e => e.From == vid)
                            .Select(e => (object)new { src = e.From, dst = e.To }));
                    }
                }

                return Json(new
                {
                    success = true,
                    data = rows,
                    columns = Array.Empty<string>(),
                    row_count = rows.Count
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

            // 顶点 / 边写入、删除
            return Json(new { success = true });
        }

        private static HttpResponseMessage Json(object payload) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        /// <summary>请求体是 JSON：取出 query 字段的 nGQL 原文（兼容直接传 nGQL 的场景）。</summary>
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
