using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Api.GraphSyncSky;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeEavSky;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// R6 关系值图投影：schema 按 relation_type 动态建边、单条 upsert/delete、
/// 按类别与全量重建载荷、消费者去重（同 ValueId 的 upsert/delete 分组）。
/// 使用 InMemory 库 + 伪 NebulaGraph REST，不依赖图数据库。
/// </summary>
public class RelationSyncTests
{
    // ============================================================
    // 单条同步
    // ============================================================

    [Fact]
    public async Task SyncRelation_Insert_UpsertsEdgeWithProps()
    {
        var handler = new FakeNebulaHandler();
        await using var db = CreateDb(Guid.NewGuid().ToString());
        var sync = CreateSynchronizer(db, handler);

        await sync.SyncRelationAsync(NewEvent(RelationChangeKind.Insert, name: "主边"));

        var edge = Assert.Single(RecordedEdges(handler));
        Assert.Equal("belongs_to", edge.Type);
        Assert.Equal("from-1", edge.Src);
        Assert.Equal("to-1", edge.Dst);
        Assert.Equal("value-1", edge.Props!["value_id"].GetString());
        Assert.Equal("attr-1", edge.Props["attribute_id"].GetString());
        Assert.Equal("主边", edge.Props["name"].GetString());
    }

    [Fact]
    public async Task SyncRelation_Update_NullNameOmitsProperty()
    {
        var handler = new FakeNebulaHandler();
        await using var db = CreateDb(Guid.NewGuid().ToString());
        var sync = CreateSynchronizer(db, handler);

        await sync.SyncRelationAsync(NewEvent(RelationChangeKind.Update, name: null));

        var edge = Assert.Single(RecordedEdges(handler));
        Assert.Equal("belongs_to", edge.Type);
        Assert.False(edge.Props!.ContainsKey("name"));
    }

    [Fact]
    public async Task SyncRelation_Delete_DeletesEdge()
    {
        var handler = new FakeNebulaHandler();
        await using var db = CreateDb(Guid.NewGuid().ToString());
        var sync = CreateSynchronizer(db, handler);

        await sync.SyncRelationAsync(NewEvent(RelationChangeKind.Delete));

        var deleted = Assert.Single(RecordedEdgeDeletes(handler));
        Assert.Equal("belongs_to", deleted.Type);
        Assert.Equal("from-1", deleted.Src);
        Assert.Equal("to-1", deleted.Dst);
        Assert.Empty(RecordedEdges(handler));
    }

    // ============================================================
    // 按类别 / 全量重建
    // ============================================================

    [Fact]
    public async Task SyncCategoryEdge_BulkIncludesRelationEdges()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateDb(dbName,
            ("n1", "n2", "feeds", "v1", "a1", "第一"),
            ("n2", "n3", "feeds", "v2", "a1", null));
        var handler = new FakeNebulaHandler();

        await CreateSynchronizer(db, handler)
            .SyncCategoryAsync(SyncCategory.Edge, CancellationToken.None);

        var edge = FindBulkEdge(handler, "v1");
        Assert.Equal("n1", edge.GetProperty("src_vid").GetString());
        Assert.Equal("n2", edge.GetProperty("dst_vid").GetString());
        Assert.Equal("feeds", edge.GetProperty("edge_type").GetString());
    }

    [Fact]
    public async Task RebuildAll_IncludesRelationEdgesAndCountsSemanticEdges()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateDb(dbName, ("n1", "n2", "feeds", "v1", "a1", "重建"));
        var handler = new FakeNebulaHandler();
        var initializer = await CreateReadyInitializerAsync(dbName, handler);
        var options = Options.Create(new GraphSyncOptions { Space = "test" });
        var sync = new GraphSynchronizer(db, CreateClient(handler, options),
            initializer, options, NullLogger<GraphSynchronizer>.Instance);

        var result = await sync.RebuildAllAsync(CancellationToken.None);

        Assert.Equal(1, result.SemanticEdges);
        Assert.Contains(handler.Requests, r => r.Body?.Contains("DROP SPACE") == true);
        var edge = FindBulkEdge(handler, "v1");
        Assert.Equal("feeds", edge.GetProperty("edge_type").GetString());
        Assert.Equal("重建", edge.GetProperty("properties").GetProperty("name").GetString());
    }

    // ============================================================
    // schema 初始化
    // ============================================================

    [Fact]
    public async Task Schema_CreatesEdgeTypeForRelationAttribute()
    {
        var dbName = Guid.NewGuid().ToString();
        var handler = new FakeNebulaHandler();
        await using (var seed = CreateDb(dbName))
        {
            seed.AttributeCatalog.Add(RelationDefinition("belongs_to"));
            await seed.SaveChangesAsync();
        }

        await CreateReadyInitializerAsync(dbName, handler);

        var create = Assert.Single(handler.Queries,
            q => q.Contains("CREATE EDGE IF NOT EXISTS `belongs_to`"));
        Assert.Contains("`value_id` string NULL", create);
        Assert.Contains("`attribute_id` string NULL", create);
        Assert.Contains("`name` string NULL", create);

        // 列已齐备 → 不做 ALTER
        Assert.DoesNotContain(handler.QueryTexts,
            q => q.Contains("ALTER EDGE `belongs_to`"));
    }

    [Fact]
    public async Task Schema_AltersLegacyEdgeMissingColumns()
    {
        var dbName = Guid.NewGuid().ToString();
        // 旧图空间：该边只有 value_id 列
        var handler = new FakeNebulaHandler(new Dictionary<string, string[]>
        {
            ["belongs_to"] = ["value_id"]
        });
        await using (var seed = CreateDb(dbName))
        {
            seed.AttributeCatalog.Add(RelationDefinition("belongs_to"));
            await seed.SaveChangesAsync();
        }

        await CreateReadyInitializerAsync(dbName, handler);

        var alter = Assert.Single(handler.QueryTexts,
            q => q.Contains("ALTER EDGE `belongs_to` ADD"));
        Assert.Contains("`attribute_id` string NULL", alter);
        Assert.Contains("`name` string NULL", alter);
        Assert.DoesNotContain("`value_id` string NULL", alter);
    }

    // ============================================================
    // 消费者
    // ============================================================

    [Fact]
    public async Task Consumer_DedupesUpsert_ButStillProcessesDelete()
    {
        var dbName = Guid.NewGuid().ToString();
        var handler = new FakeNebulaHandler();
        var options = Options.Create(new GraphSyncOptions
        {
            Space = "test",
            DedupWindowSeconds = 60
        });
        var cache = new MemoryCache(new MemoryCacheOptions());

        var initializer = await CreateReadyInitializerAsync(dbName, handler);
        var services = new ServiceCollection();
        services.AddSingleton(initializer);
        services.AddScoped<TreeGraphDbContext>(_ => new TestDbContext(NewOptions(dbName)));
        services.AddScoped(sp => new GraphSynchronizer(
            sp.GetRequiredService<TreeGraphDbContext>(),
            CreateClient(handler, options),
            initializer, options, NullLogger<GraphSynchronizer>.Instance));
        await using var provider = services.BuildServiceProvider();

        var consumer = new RelationGraphSyncConsumer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            cache, options, NullLogger<RelationGraphSyncConsumer>.Instance);

        var evt = NewEvent(RelationChangeKind.Insert);
        await consumer.Consume(evt, CancellationToken.None);
        await consumer.Consume(evt, CancellationToken.None); // 窗口内重复 → 跳过

        Assert.Single(RecordedEdges(handler));

        // 同 ValueId 的 Delete 不受 upsert 去重影响
        await consumer.Consume(
            NewEvent(RelationChangeKind.Delete), CancellationToken.None);
        Assert.Single(RecordedEdgeDeletes(handler));
    }

    // ============================================================
    // helpers
    // ============================================================

    private static RelationChangedEvent NewEvent(
        RelationChangeKind kind, string? name = "边名") => new()
    {
        ValueId = "value-1",
        AttributeId = "attr-1",
        RelationType = "belongs_to",
        FromNodeId = "from-1",
        ToNodeId = "to-1",
        Name = name,
        Kind = kind
    };

    private static AttributeDefinition RelationDefinition(string relationType) => new()
    {
        AttributeId = Guid.NewGuid().ToString(),
        EntityType = "reltest",
        AttributeName = $"rel_{Guid.NewGuid():N}",
        DisplayName = "关系",
        DataType = EavDataTypes.Relation,
        RelationType = relationType
    };

    private static DbContextOptions<TreeGraphDbContext> NewOptions(string dbName)
        => new DbContextOptionsBuilder<TreeGraphDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

    private static TestDbContext CreateDb(
        string dbName,
        params (string From, string To, string Type, string ValueId,
            string AttributeId, string? Name)[] relations)
    {
        var db = new TestDbContext(NewOptions(dbName));
        foreach (var r in relations)
        {
            db.RelationAttributeValues.Add(new RelationAttributeValue
            {
                ValueId = r.ValueId,
                EntityId = r.From,
                EntityType = "reltest",
                AttributeId = r.AttributeId,
                RelationType = r.Type,
                ToNodeId = r.To,
                Name = r.Name
            });
        }

        db.SaveChanges();
        return db;
    }

    private static NebulaGraphClient CreateClient(
        FakeNebulaHandler handler, IOptions<GraphSyncOptions> options)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://nebula.test/") };
        return new NebulaGraphClient(http, options, NullLogger<NebulaGraphClient>.Instance);
    }

    private static GraphSynchronizer CreateSynchronizer(
        TreeGraphDbContext db, FakeNebulaHandler handler)
    {
        var options = Options.Create(new GraphSyncOptions { Space = "test" });
        return new GraphSynchronizer(db, CreateClient(handler, options),
            null!, options, NullLogger<GraphSynchronizer>.Instance);
    }

    private static async Task<GraphSchemaInitializer> CreateReadyInitializerAsync(
        string dbName, FakeNebulaHandler handler)
    {
        var options = Options.Create(new GraphSyncOptions { Space = "test" });
        var services = new ServiceCollection();
        services.AddScoped<TreeGraphDbContext>(_ => new TestDbContext(NewOptions(dbName)));
        // 生命周期覆盖整个测试：initializer 持有该 provider 的 scope factory
        var provider = services.BuildServiceProvider();

        var initializer = new GraphSchemaInitializer(
            CreateClient(handler, options),
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<GraphSchemaInitializer>.Instance);

        await initializer.StartAsync(CancellationToken.None);
        await initializer.EnsureReadyAsync().WaitAsync(TimeSpan.FromSeconds(30));
        return initializer;
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
                var props = root.TryGetProperty("properties", out var p)
                            && p.ValueKind == JsonValueKind.Object
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

    private static JsonElement FindBulkEdge(FakeNebulaHandler handler, string valueId)
    {
        var bulk = handler.Requests.Single(r => r.Path.EndsWith("/graph/bulk"));
        using var doc = JsonDocument.Parse(bulk.Body!);
        return doc.RootElement.GetProperty("edges").EnumerateArray()
            .Single(e => e.GetProperty("properties").TryGetProperty("value_id", out var v)
                         && v.GetString() == valueId)
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
    /// 伪 NebulaGraph REST：写入返回 success；DESCRIBE 按预置列返回，用于验证补列。
    /// </summary>
    private sealed class FakeNebulaHandler(
        IReadOnlyDictionary<string, string[]>? describedEdges = null) : HttpMessageHandler
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
                    var edgeName = ngql.Split('`').Skip(1).FirstOrDefault() ?? "";
                    var fields = describedEdges is not null
                                 && describedEdges.TryGetValue(edgeName, out var cols)
                        ? cols
                        : ["value_id", "attribute_id", "name"];
                    var rows = fields.Select(f => (object)new { Field = f }).ToList();
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
