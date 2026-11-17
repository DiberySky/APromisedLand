using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Contracts.Messaging;
using TreeGraph.Persistence.Data;
using TreeGraph.Persistence.SpaceNodeSky.Entities;
using TreeGraph.Shared.NodeSky.Entities;
using Wolverine.EntityFrameworkCore;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

[Collection("MessagingOutbox")]
public sealed class MessagingOutboxIntegrationTests : IAsyncLifetime
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
    private MessagingFactory _factory = null!;
    private HttpClient _client = null!;
    private bool _rabbitPaused;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbit.StartAsync();
        _factory = new MessagingFactory(
            _postgres.GetConnectionString(),
            $"amqp://guest:guest@{_rabbit.Hostname}:{_rabbit.GetMappedPublicPort(5672)}/");
        _client = _factory.CreateClient();
        _client.Timeout = TimeSpan.FromSeconds(90);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        if (_rabbitPaused)
        {
            try { await _rabbit.UnpauseAsync(); }
            catch { /* 容器已停止时无需额外清理。 */ }
        }
        await _rabbit.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Write_request_rolls_back_atomically_and_outbox_survives_rabbit_pause()
    {
        var rolledBackId = Guid.NewGuid().ToString();
        var rollback = await _client.PostAsJsonAsync(
            "/__messaging-test/write", new TestWriteRequest(rolledBackId, Rollback: true));
        Assert.Equal(HttpStatusCode.BadRequest, rollback.StatusCode);
        Assert.False(await NodeExistsAsync(rolledBackId));
        Assert.Equal(0, await OutgoingCountAsync());
        Assert.Equal(0, _factory.PostCommitProbe.Count);
        await VerifyTagProjectionCleanupAsync();

        await _rabbit.PauseAsync();
        _rabbitPaused = true;
        var committedId = Guid.NewGuid().ToString();
        var commit = await _client.PostAsJsonAsync(
            "/__messaging-test/write", new TestWriteRequest(committedId, Rollback: false));
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);

        Assert.True(await NodeExistsAsync(committedId));
        Assert.Equal(1, _factory.PostCommitProbe.Count);
        Assert.Equal(1, await WaitForOutgoingCountAsync(expected: 1, timeoutSeconds: 20));

        await _rabbit.UnpauseAsync();
        _rabbitPaused = false;
        Assert.Equal(0, await WaitForOutgoingCountAsync(expected: 0, timeoutSeconds: 45));
    }

    private async Task<bool> NodeExistsAsync(string id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
        return await db.Nodes.AsNoTracking().AnyAsync(x => x.Id == id);
    }

    private async Task VerifyTagProjectionCleanupAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
        var spaceIds = await db.Spaces.AsNoTracking().OrderBy(x => x.Id)
            .Take(2).Select(x => x.Id).ToListAsync();
        Assert.Equal(2, spaceIds.Count);

        var suffix = Guid.NewGuid().ToString("N");
        var targetTag = $"target_{suffix}";
        db.TagProjections.AddRange(
            Projection(spaceIds[0], targetTag, "source-a", 2, true),
            Projection(spaceIds[0], $"active_{suffix}", "source-a", 2, false),
            Projection(spaceIds[1], targetTag, "source-a", 1, true),
            Projection(spaceIds[1], $"other_{suffix}", "source-b", 2, true));
        await db.SaveChangesAsync();
        var command = new TagProjectionCleanupRequested
        {
            SpaceId = spaceIds[0], SourceAttributeId = "source-a",
            TagName = targetTag, Version = 2
        };

        await TagProjectionCleanupHandler.Handle(command, db, CancellationToken.None);
        await TagProjectionCleanupHandler.Handle(command, db, CancellationToken.None);

        var remaining = await db.TagProjections.AsNoTracking()
            .Where(x => x.TagName.EndsWith(suffix)).ToListAsync();
        Assert.Equal(3, remaining.Count);
        Assert.DoesNotContain(remaining, x =>
            x.SpaceId == command.SpaceId && x.TagName == command.TagName);
    }

    private static TagProjection Projection(
        string spaceId, string tagName, string sourceId, int version, bool isDeleted)
        => new()
        {
            SpaceId = spaceId, TagName = tagName, SourceAttributeId = sourceId,
            SourceEntityType = "test", SchemaSnapshot = "{}",
            Version = version, IsDeleted = isDeleted
        };

    private Task<int> OutgoingCountAsync() => QueryOutgoingCountAsync(_postgres.GetConnectionString());

    private async Task<int> WaitForOutgoingCountAsync(int expected, int timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var count = -1;
        while (DateTime.UtcNow < deadline)
        {
            count = await OutgoingCountAsync();
            if (count == expected) return count;
            await Task.Delay(250);
        }
        return count;
    }

    private static async Task<int> QueryOutgoingCountAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM wolverine.wolverine_outgoing_envelopes", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed class MessagingFactory(string database, string rabbit) : WebApplicationFactory<Program>
    {
        public PostCommitProbe PostCommitProbe { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("MessagingIntegration");
            builder.UseSetting("ConnectionStrings:TreeGraphDb", database);
            builder.UseSetting("ConnectionStrings:RabbitMQ", rabbit);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(PostCommitProbe);
                services.AddControllers().AddApplicationPart(typeof(MessagingTestController).Assembly);
            });
        }
    }
}

[CollectionDefinition("MessagingOutbox", DisableParallelization = true)]
public sealed class MessagingOutboxCollection;

public sealed class PostCommitProbe
{
    private int _count;
    public int Count => Volatile.Read(ref _count);
    public void Increment() => Interlocked.Increment(ref _count);
}

public sealed record TestWriteRequest(string Id, bool Rollback);

[ApiController]
[Route("__messaging-test")]
public sealed class MessagingTestController(
    TreeGraphDbContext db,
    IDbContextOutbox outbox,
    TreeGraphRequestTransaction requestTransaction,
    PostCommitProbe probe) : ControllerBase
{
    [HttpPost("write")]
    public async Task<IActionResult> Write(TestWriteRequest request, CancellationToken ct)
    {
        db.Nodes.Add(new Node { Id = request.Id, Name = request.Id, HostType = "Node" });
        await db.SaveChangesAsync(ct);
        await outbox.PublishAsync(new InodeChangedEvent
        {
            InodeId = request.Id,
            Kind = InodeChangeKind.Saved
        });
        await requestTransaction.AfterCommitAsync(_ =>
        {
            probe.Increment();
            return Task.CompletedTask;
        }, ct);

        return request.Rollback ? BadRequest() : Ok();
    }
}
