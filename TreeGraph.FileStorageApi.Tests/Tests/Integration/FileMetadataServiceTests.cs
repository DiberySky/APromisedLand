using Microsoft.EntityFrameworkCore;
using TreeGraph.FileStorageApi.Entities;
using TreeGraph.FileStorageApi.Files;
using TreeGraph.FileStorageApi.Tests.Fixtures;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests.Integration;

/// <summary>
/// FileMetadataService 服务级集成测试：
/// 查询（分页/过滤/版本选择）、下载（active 限定 + 审计）、删除两阶段。
/// </summary>
public sealed class FileMetadataServiceTests : ServiceTestBase
{
    public FileMetadataServiceTests(FileStorageApiFactory factory) : base(factory) { }

    private IFileMetadataService Files => Svc<IFileMetadataService>();

    private DocumentMetadataEntity Meta(
        string tenant, string docId, int version,
        string status = "active", DateTimeOffset? createdAt = null)
        => new()
        {
            DocId = docId,
            Tenant = tenant,
            Version = version,
            FileName = $"{docId}-v{version}.bin",
            ContentType = "application/octet-stream",
            Size = 100 * version,
            ObjectKey = $"{tenant}/{docId}/v{version}/f.bin",
            Status = status,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private async Task<DocumentMetadataEntity> SeedAsync(
        DocumentMetadataEntity meta, byte[]? bytes = null)
    {
        Db.DocumentMetadata.Add(meta);
        await Db.SaveChangesAsync();
        if (bytes is not null)
            await Storage.PutAsync(meta.ObjectKey, new MemoryStream(bytes), bytes.Length, meta.ContentType);
        return meta;
    }

    // ══════════ 维度 1：查询 ══════════

    [Fact]
    public async Task List_FiltersByTenant_ExcludesDeleted_OrdersByCreatedDesc()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        var a = await SeedAsync(Meta("t1", "doc-a", 1, createdAt: baseTime));
        var b = await SeedAsync(Meta("t1", "doc-b", 1, createdAt: baseTime.AddMinutes(1)));
        await SeedAsync(Meta("t1", "doc-c", 1, status: "deleted", createdAt: baseTime.AddMinutes(2)));
        await SeedAsync(Meta("t2", "doc-d", 1, createdAt: baseTime.AddMinutes(3)));

        var list = await Files.ListAsync(0, 50, CancellationToken.None);

        Assert.Equal([b.Id, a.Id], list.Select(m => m.Id));   // 新→旧，排除 deleted 与他租户
    }

    [Fact]
    public async Task List_PaginationAndClamps()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var baseTime = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
            await SeedAsync(Meta("t1", $"doc-{i}", 1, createdAt: baseTime.AddMinutes(i)));

        var page = await Files.ListAsync(skip: 1, take: 2, CancellationToken.None);
        Assert.Equal(2, page.Count);

        // take=0 → 钳为 1；take 超上限 → 钳为 200
        Assert.Single(await Files.ListAsync(0, 0, CancellationToken.None));
        Assert.Equal(5, (await Files.ListAsync(0, 1000, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task GetById_FiltersByTenant_ButReturnsDeletedStatusRow()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var mine = await SeedAsync(Meta("t1", "doc-1", 1));
        await SeedAsync(Meta("t1", "doc-2", 1, status: "deleted"));
        var other = await SeedAsync(Meta("t2", "doc-3", 1));

        Assert.NotNull(await Files.GetByIdAsync(mine.Id, CancellationToken.None));
        Assert.NotNull(await Files.GetByIdAsync(
            await Db.DocumentMetadata.Where(m => m.Status == "deleted")
                .Select(m => m.Id).SingleAsync(), CancellationToken.None));
        Assert.Null(await Files.GetByIdAsync(other.Id, CancellationToken.None));
        Assert.Null(await Files.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetByDocId_DefaultLatestVersion_SpecificVersion()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        await SeedAsync(Meta("t1", "doc-1", 1));
        var v2 = await SeedAsync(Meta("t1", "doc-1", 2));
        await SeedAsync(Meta("t1", "doc-1", 3, status: "deleted"));

        var latest = await Files.GetByDocIdAsync("doc-1", null, CancellationToken.None);
        Assert.Equal(2, latest!.Version);   // deleted 的 v3 被排除

        var specific = await Files.GetByDocIdAsync("doc-1", 1, CancellationToken.None);
        Assert.Equal(1, specific!.Version);

        // deleted / delete_pending 一律查不到
        Assert.Null(await Files.GetByDocIdAsync("doc-1", 3, CancellationToken.None));
        Assert.Null(await Files.GetByDocIdAsync("missing", null, CancellationToken.None));
    }

    // ══════════ 维度 2：下载 ══════════

    [Fact]
    public async Task Download_ActiveObject_ReturnsStreamAndMetadata()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var meta = await SeedAsync(Meta("t1", "doc-1", 1), payload);

        var result = await Files.DownloadAsync(meta.Id, null, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(payload, ((MemoryStream)result!.Content).ToArray());
        Assert.Equal(5, result.ContentLength);
        Assert.Equal(5, result.TotalLength);
        Assert.Equal(meta.Id, result.Metadata.Id);
    }

    [Fact]
    public async Task Download_NonActiveStatusOrMissingObject_ReturnsNull()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var pending = await SeedAsync(Meta("t1", "doc-1", 1, status: "pending_upload"));

        // 非 active → null
        Assert.Null(await Files.DownloadAsync(pending.Id, null, null, CancellationToken.None));

        // active 但对象丢失 → null（不抛异常）
        var noObject = await SeedAsync(Meta("t1", "doc-2", 1));
        Assert.Null(await Files.DownloadAsync(noObject.Id, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Download_Range_ReturnsSlicedContent()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var meta = await SeedAsync(Meta("t1", "doc-1", 1), payload);

        var result = await Files.DownloadAsync(meta.Id, 2, 4, CancellationToken.None);

        Assert.Equal(3, result!.ContentLength);
        Assert.Equal(10, result.TotalLength);
        Assert.Equal("bytes 2-4/10", result.ContentRange);
        Assert.Equal(new byte[] { 3, 4, 5 }, ((MemoryStream)result.Content).ToArray());
    }

    [Fact]
    public async Task Download_EnqueuesAuditRow()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        Caller.Actor = "downloader";
        var meta = await SeedAsync(Meta("t1", "doc-1", 1), new byte[] { 1 });

        await Files.DownloadAsync(meta.Id, null, null, CancellationToken.None);

        // 审计走后台队列 → 轮询等待落库
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var exists = await Db.DocumentAudits.AnyAsync(a =>
                a.DocId == "doc-1" && a.Action == "downloaded" && a.Actor == "downloader");
            if (exists) return;
            await Task.Delay(200);
        }
        Assert.Fail("下载审计行未在 10 秒内落库。");
    }

    // ══════════ 维度 3：删除两阶段 ══════════

    [Fact]
    public async Task Delete_S3Success_GoesToDeleted_AndWritesAudit()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        Caller.Actor = "deleter";
        var key = "t1/doc-1/v1/f.bin";
        await Storage.PutAsync(key, new MemoryStream(new byte[10]), 10, "application/octet-stream");
        var meta = await SeedAsync(Meta("t1", "doc-1", 1), new byte[10]);

        Assert.True(await Files.DeleteAsync(meta.Id, CancellationToken.None));

        var deleted = await Db.DocumentMetadata.AsNoTracking().SingleAsync(m => m.Id == meta.Id);
        Assert.Equal("deleted", deleted.Status);
        Assert.Null(Storage.GetBytes(key));

        var audit = await Db.DocumentAudits.AsNoTracking()
            .SingleAsync(a => a.DocId == "doc-1" && a.Action == "deleted");
        Assert.Equal("deleter", audit.Actor);
    }

    [Fact]
    public async Task Delete_S3Failure_StaysDeletePending()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var key = "t1/doc-2/v1/f.bin";
        await Storage.PutAsync(key, new MemoryStream(new byte[10]), 10, "application/octet-stream");
        var meta = await SeedAsync(Meta("t1", "doc-2", 1), new byte[10]);

        Storage.DeleteOverride = _ => Task.FromException<bool>(new IOException("S3 down"));

        Assert.True(await Files.DeleteAsync(meta.Id, CancellationToken.None));

        var pending = await Db.DocumentMetadata.AsNoTracking().SingleAsync(m => m.Id == meta.Id);
        Assert.Equal("delete_pending", pending.Status);
        Assert.NotNull(Storage.GetBytes(key));

        // S3 失败也写审计（s3Ok=false）
        var audit = await Db.DocumentAudits.AsNoTracking()
            .SingleAsync(a => a.DocId == "doc-2" && a.Action == "deleted");
        Assert.Contains("\"s3Ok\":false", audit.DetailsJson);
    }

    [Fact]
    public async Task Delete_UnknownOrCrossTenant_ReturnsFalse()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var other = await SeedAsync(Meta("t2", "doc-1", 1));

        Assert.False(await Files.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.False(await Files.DeleteAsync(other.Id, CancellationToken.None));
    }

    // ── 维度 1 补充：边界与过滤 ──

    [Fact]
    public async Task GetByDocId_ExcludesDeletePendingStatus()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var v1 = await SeedAsync(Meta("t1", "doc-1", 1));
        await SeedAsync(Meta("t1", "doc-1", 2, status: "delete_pending"));
        await SeedAsync(Meta("t1", "doc-1", 3, status: "deleted"));

        // 无 version：v2/v3 被排除，最新非 deleted/delete_pending 即 v1
        var latest = await Files.GetByDocIdAsync("doc-1", null, CancellationToken.None);
        Assert.Equal(v1.Id, latest!.Id);
        Assert.Equal(1, latest.Version);

        // 指定 version=2：delete_pending 被排除 → null
        Assert.Null(await Files.GetByDocIdAsync("doc-1", 2, CancellationToken.None));

        // 指定 version=3：deleted 被排除 → null
        Assert.Null(await Files.GetByDocIdAsync("doc-1", 3, CancellationToken.None));
    }

    [Fact]
    public async Task List_NegativeSkip_ClampedToZero()
    {
        await WipeDbAsync();
        Caller.Tenant = "t1";
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        var a = await SeedAsync(Meta("t1", "doc-a", 1, createdAt: baseTime));
        var b = await SeedAsync(Meta("t1", "doc-b", 1, createdAt: baseTime.AddMinutes(1)));

        // skip=-5 钳为 0，不跳过任何行；按 CreatedAt 降序
        var list = await Files.ListAsync(skip: -5, take: 50, CancellationToken.None);

        Assert.Equal(2, list.Count);
        Assert.Equal([b.Id, a.Id], list.Select(m => m.Id));
    }
}
