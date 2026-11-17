using System.Net;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using TreeGraph.Contracts.Messaging;
using TreeGraph.GraphSync;
using TreeGraph.Persistence.Data;
using TreeGraph.Persistence.SpaceNodeSky.Entities;
using TreeGraph.Shared.NodeSky.Entities;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.SyncServices.GraphSync;
using TreeGraph.SyncServices.SemanticSearch;
using Wolverine;
using Wolverine.RabbitMQ;

namespace TreeGraph.SyncServices.Tests.Tests;

[Collection("SyncServicesHost")]
public sealed class SyncServicesHostIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("TreeGraphDb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private readonly IContainer _rabbit = new ContainerBuilder("rabbitmq:4-alpine")
        .WithPortBinding(5672, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
        .Build();
    private readonly IContainer _qdrant = new ContainerBuilder("qdrant/qdrant:latest")
        .WithPortBinding(6333, true)
        .WithPortBinding(6334, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
            request => request.ForPort(6333).ForPath("/readyz")))
        .Build();
    private readonly RecordingNebulaHandler _nebula = new();
    private readonly FailOnceEmbeddingHandler _embedding = new();
    private SyncFactory _factory = null!;
    private HttpClient _client = null!;
    private IConnection _rabbitConnection = null!;
    private IChannel _rabbitChannel = null!;
    private string _rabbitUri = "";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync(), _qdrant.StartAsync());
        _rabbitUri = $"amqp://guest:guest@{_rabbit.Hostname}:{_rabbit.GetMappedPublicPort(5672)}/";

        var dbOptions = new DbContextOptionsBuilder<TreeGraphDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using (var db = new TreeGraphDbContext(dbOptions))
            await db.Database.EnsureCreatedAsync();

        _factory = new SyncFactory(
            _postgres.GetConnectionString(),
            _rabbitUri,
            $"http://{_qdrant.Hostname}:{_qdrant.GetMappedPublicPort(6334)}",
            _nebula,
            _embedding);
        _client = _factory.CreateClient();
        _client.Timeout = TimeSpan.FromSeconds(90);

        var connectionFactory = new ConnectionFactory { Uri = new Uri(_rabbitUri) };
        _rabbitConnection = await connectionFactory.CreateConnectionAsync();
        _rabbitChannel = await _rabbitConnection.CreateChannelAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitChannel.DisposeAsync();
        await _rabbitConnection.DisposeAsync();
        _client.Dispose();
        await _factory.DisposeAsync();
        await _qdrant.DisposeAsync();
        await _rabbit.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Host_consumes_retries_replays_and_publishes_cleanup_through_real_transports()
    {
        var health = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var parts = _factory.Services.GetRequiredService<ApplicationPartManager>();
        Assert.DoesNotContain(parts.ApplicationParts.OfType<AssemblyPart>(),
            part => part.Assembly.GetName().Name == "TreeGraph.Api");

        var businessQueues = new[]
        {
            MessagingQueueNames.InodeChanged,
            MessagingQueueNames.EdgeChanged,
            MessagingQueueNames.EntityChanged,
            MessagingQueueNames.TagProjectionChanged
        };
        await WaitUntilAsync(async () =>
        {
            foreach (var queue in businessQueues)
            {
                var state = await _rabbitChannel.QueueDeclarePassiveAsync(queue);
                if (state.ConsumerCount == 0) return false;
            }
            return (await _rabbitChannel.QueueDeclarePassiveAsync(
                MessagingQueueNames.TagProjectionCleanup)).ConsumerCount == 0;
        }, "四个业务 listener 与反向清理发送端点未就绪");

        await _factory.Services.GetRequiredService<GraphSchemaInitializer>()
            .EnsureReadyAsync().WaitAsync(TimeSpan.FromSeconds(30));
        await _factory.Services.GetRequiredService<QdrantInitializer>()
            .EnsureReadyAsync().WaitAsync(TimeSpan.FromSeconds(30));

        var inodeId = await SeedUnitNodeAsync();
        await using var busScope = _factory.Services.CreateAsyncScope();
        var bus = busScope.ServiceProvider.GetRequiredService<IMessageBus>();
        var inodeEndpoint = bus.EndpointFor(RabbitMqEndpointUri.Queue(MessagingQueueNames.InodeChanged));
        await inodeEndpoint.SendAsync(new InodeChangedEvent
        {
            InodeId = inodeId, Kind = InodeChangeKind.Saved
        });

        await WaitUntilAsync(async () =>
            _embedding.Attempts >= 2 && await NodePointExistsAsync(inodeId),
            "首次 Embedding 失败后未发生持久化重试，或向量点未写入");
        await WaitUntilAsync(() => Task.FromResult(_nebula.VertexWriteCount(inodeId) >= 1),
            "向量重试成功后，同消息的图投影未执行");
        var graphWritesBeforeDuplicates = _nebula.VertexWriteCount(inodeId);

        // 新业务消息使用不同 envelope id，模拟业务重复与 Deleted/Saved 乱序；
        // 两侧始终按 PG 当前态投影，最终仍只有同一个确定性向量点。
        await inodeEndpoint.SendAsync(new InodeChangedEvent
        {
            InodeId = inodeId, Kind = InodeChangeKind.Saved
        });
        await inodeEndpoint.SendAsync(new InodeChangedEvent
        {
            InodeId = inodeId, Kind = InodeChangeKind.Deleted,
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await WaitUntilAsync(() => NodePointExistsAsync(inodeId), "重复/乱序事件后的向量投影不收敛");
        await WaitUntilAsync(() => Task.FromResult(
                _nebula.VertexWriteCount(inodeId) >= graphWritesBeforeDuplicates + 2),
            "重复/乱序事件未幂等重放图投影");
        Assert.Single(await RetrieveNodePointsAsync(inodeId));

        var spaceId = SeedSpaceIds.Unit;
        var suffix = Guid.NewGuid().ToString("N");
        var sourceId = $"s{suffix}";
        var tagName = $"tag_{suffix}";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
            db.TagProjections.Add(new TagProjection
            {
                SpaceId = spaceId, SourceAttributeId = sourceId,
                SourceEntityType = "integration", TagName = tagName,
                SchemaSnapshot = "{}", Version = 3, IsDeleted = true,
                DeletedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var tagEndpoint = bus.EndpointFor(RabbitMqEndpointUri.Queue(
            MessagingQueueNames.TagProjectionChanged));
        await tagEndpoint.SendAsync(new SpaceNodeTagProjectionChangedEvent
        {
            SpaceId = spaceId, SourceAttributeId = sourceId,
            Kind = SpaceNodeTagProjectionChangeKind.Deleted, Version = 3
        });

        BasicGetResult? cleanup = null;
        await WaitUntilAsync(async () =>
        {
            cleanup = await _rabbitChannel.BasicGetAsync(
                MessagingQueueNames.TagProjectionCleanup, autoAck: false);
            return cleanup is not null;
        }, "图侧 DROP 成功后未向核心清理队列发送命令");
        var cleanupBody = Encoding.UTF8.GetString(cleanup!.Body.ToArray());
        Assert.Contains(spaceId, cleanupBody);
        Assert.Contains(sourceId, cleanupBody);
        Assert.Contains(tagName, cleanupBody);
        Assert.Contains("3", cleanupBody);
        await _rabbitChannel.BasicAckAsync(cleanup.DeliveryTag, false);

        // 账本已升至 v3，迟到的 v2 不得再次 DROP/发送清理命令。
        await tagEndpoint.SendAsync(new SpaceNodeTagProjectionChangedEvent
        {
            SpaceId = spaceId, SourceAttributeId = sourceId,
            Kind = SpaceNodeTagProjectionChangeKind.Deleted, Version = 2,
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Null(await _rabbitChannel.BasicGetAsync(
            MessagingQueueNames.TagProjectionCleanup, autoAck: true));
    }

    private async Task<string> SeedUnitNodeAsync()
    {
        var id = Guid.NewGuid().ToString();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
        db.Nodes.Add(new Node { Id = id, HostType = "UnitTree", Name = "集成测试单位" });
        db.UnitTrees.Add(new UnitTree
        {
            Id = id, Abbreviation = "IT", CanHaveChildren = true
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<bool> NodePointExistsAsync(string inodeId)
        => (await RetrieveNodePointsAsync(inodeId)).Count == 1;

    private async Task<IReadOnlyList<Qdrant.Client.Grpc.RetrievedPoint>> RetrieveNodePointsAsync(string inodeId)
    {
        var client = _factory.Services.GetRequiredService<QdrantClient>();
        return await client.RetrieveAsync(
            "tree_nodes", QdrantVectorStore.NodePointUuid(inodeId, SourceTextBuilder.ViewType),
            true, false, cancellationToken: CancellationToken.None);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string failure)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(250);
        }
        Assert.Fail(failure);
    }

    private sealed class SyncFactory(
        string database, string rabbit, string qdrant,
        HttpMessageHandler nebula, HttpMessageHandler embedding) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:TreeGraphDb", database);
            builder.UseSetting("ConnectionStrings:RabbitMQ", rabbit);
            builder.UseSetting("ConnectionStrings:Qdrant", qdrant);
            builder.UseSetting("NebulaFastApi:Endpoint", "http://nebula.test/");
            builder.UseSetting("Embedding:Endpoint", "http://embedding.test/v1/");
            builder.UseSetting("SemanticSearch:EmbeddingFailureCooldownSeconds", "0");
            builder.UseSetting("VectorSync:ReconciliationIntervalHours", "0");
            builder.UseSetting("GraphSync:ReconcileIntervalSeconds", "3600");
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient<NebulaGraphClient>(client =>
                        client.BaseAddress = new Uri("http://nebula.test/"))
                    .ConfigurePrimaryHttpMessageHandler(() => nebula);
                services.AddHttpClient<EmbeddingClient>(client =>
                        client.BaseAddress = new Uri("http://embedding.test/v1/"))
                    .ConfigurePrimaryHttpMessageHandler(() => embedding);
            });
        }
    }

    private sealed class FailOnceEmbeddingHandler : HttpMessageHandler
    {
        private int _attempts;
        public int Attempts => Volatile.Read(ref _attempts);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _attempts) == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var response = new
            {
                data = new[] { new { embedding = Enumerable.Repeat(0.1f, 1024).ToArray() } }
            };
            return Task.FromResult(Json(response));
        }

        internal static HttpResponseMessage Json(object payload) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private sealed class RecordingNebulaHandler : HttpMessageHandler
    {
        private readonly List<(string Path, string? Body)> _requests = [];

        public int VertexWriteCount(string inodeId)
        {
            lock (_requests)
                return _requests.Count(x => x.Path.EndsWith("/graph/vertices")
                    && x.Body?.Contains($"\"vid\":\"{inodeId}\"") == true);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null
                ? null : await request.Content.ReadAsStringAsync(ct);
            lock (_requests)
                _requests.Add((request.RequestUri!.AbsolutePath, body));

            if (request.RequestUri.AbsolutePath.EndsWith("/graph/query"))
            {
                var query = ExtractQuery(body) ?? "";
                if (query.StartsWith("DESCRIBE TAG", StringComparison.Ordinal))
                {
                    var tag = query.Split('`').Skip(1).FirstOrDefault() ?? "";
                    var fields = GraphNaming.TagColumns.TryGetValue(tag, out var known) ? known : [];
                    return FailOnceEmbeddingHandler.Json(new
                    {
                        success = true,
                        data = fields.Select(field => new { Field = field }),
                        row_count = fields.Length
                    });
                }
                if (query.StartsWith("DESCRIBE EDGE", StringComparison.Ordinal))
                {
                    var fields = new[] { "value_id", "attribute_id", "name" };
                    return FailOnceEmbeddingHandler.Json(new
                    {
                        success = true, data = fields.Select(field => new { Field = field }),
                        row_count = fields.Length
                    });
                }
                return FailOnceEmbeddingHandler.Json(new
                {
                    success = true, data = Array.Empty<object>(), row_count = 0
                });
            }

            return FailOnceEmbeddingHandler.Json(new { success = true });
        }

        private static string? ExtractQuery(string? body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("query", out var query)
                ? query.GetString() : body;
        }
    }
}

[CollectionDefinition("SyncServicesHost", DisableParallelization = true)]
public sealed class SyncServicesHostCollection;
