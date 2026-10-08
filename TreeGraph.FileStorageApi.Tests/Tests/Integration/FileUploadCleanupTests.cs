using Microsoft.EntityFrameworkCore;
using TreeGraph.FileStorageApi.Entities;
using TreeGraph.FileStorageApi.Tests.Fixtures;
using TreeGraph.FileStorageApi.Uploads;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests.Integration;

/// <summary>
/// CleanupExpiredAsync 六段清理逻辑集成测试（真实 PostgreSQL）：
/// 1. 过期非 completed/merging 会话删除（含分块）
/// 2. completed 保留期清理
/// 3. stale merging → failed
/// 4. pending_upload 孤儿元数据 → S3 删除 + 元数据删除（含删除失败重试）
/// 5. delete_pending 重试
/// 6. failed 会话 → cleaned
/// 通过直接种子 DB 行（把时间戳拨到过去）验证各分支。
/// </summary>
public sealed class FileUploadCleanupTests : ServiceTestBase
{
    public FileUploadCleanupTests(FileStorageApiFactory factory) : base(factory) { }

    private const int Chunk = 256 * 1024;

    private IFileUploadService Uploads => Svc<IFileUploadService>();

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private UploadSessionEntity Session(
        string tenant, string status, DateTimeOffset expiresAt,
        DateTimeOffset? completedAt = null, DateTimeOffset? updatedAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Tenant = tenant,
            FileName = "a.bin",
            ContentType = "application/octet-stream",
            TotalSize = 1000,
            ChunkSize = Chunk,
            TotalChunks = 1,
            Status = status,
            CreatedAt = Now,
            UpdatedAt = updatedAt ?? Now,
            ExpiresAt = expiresAt,
            CompletedAt = completedAt,
        };

    private async Task<UploadSessionEntity> SeedAsync(
        UploadSessionEntity session, bool withChunk = false)
    {
        Db.UploadSessions.Add(session);
        if (withChunk)
        {
            Db.UploadChunks.Add(new UploadChunkEntity
            {
                UploadId = session.Id, ChunkIndex = 0, Size = 1000, Data = new byte[1000],
            });
        }
        await Db.SaveChangesAsync();
        return session;
    }

    // ── 1. 过期会话删除 ──

    [Fact]
    public async Task Cleanup_RemovesExpiredSessions_AndTheirChunks_KeepsActive()
    {
        await WipeDbAsync();

        var expired = await SeedAsync(
            Session("t1", "pending", Now.AddHours(-1)), withChunk: true);
        var active = await SeedAsync(
            Session("t1", "uploading", Now.AddHours(10)), withChunk: true);

        var removed = await Uploads.CleanupExpiredAsync(CancellationToken.None);

        Assert.True(removed >= 1);
        Assert.False(await Db.UploadSessions.AnyAsync(s => s.Id == expired.Id));
        Assert.False(await Db.UploadChunks.AnyAsync(c => c.UploadId == expired.Id));
        Assert.True(await Db.UploadSessions.AnyAsync(s => s.Id == active.Id));
    }

    [Fact]
    public async Task Cleanup_DoesNotRemoveExpiredCompletedOrMergingSessions()
    {
        await WipeDbAsync();

        // completed 过期只走"保留期"分支；merging 只走 stale 标记分支
        var completed = await SeedAsync(Session(
            "t1", "completed", Now.AddHours(-1), completedAt: Now.AddHours(-1)));
        var merging = await SeedAsync(Session("t2", "merging", Now.AddHours(-1)));

        await Uploads.CleanupExpiredAsync(CancellationToken.None);

        Assert.True(await Db.UploadSessions.AnyAsync(s => s.Id == completed.Id));
        Assert.Equal("merging", await Db.UploadSessions
            .Where(s => s.Id == merging.Id).Select(s => s.Status).SingleAsync());
    }

    // ── 2. completed 保留期 ──

    [Fact]
    public async Task Cleanup_RemovesCompletedSessionsBeyondRetention_KeepsRecent()
    {
        await WipeDbAsync();

        var old = await SeedAsync(Session(
            "t1", "completed", Now.AddHours(1),
            completedAt: Now.AddDays(-10)));    // 默认保留 7 天 → 超期
        var recent = await SeedAsync(Session(
            "t1", "completed", Now.AddHours(1),
            completedAt: Now.AddDays(-1)));     // 未超期

        await Uploads.CleanupExpiredAsync(CancellationToken.None);

        Assert.False(await Db.UploadSessions.AnyAsync(s => s.Id == old.Id));
        Assert.True(await Db.UploadSessions.AnyAsync(s => s.Id == recent.Id));
    }

    // ── 3. stale merging → failed ──

    [Fact]
    public async Task Cleanup_MarksStaleMergingAsFailed()
    {
        await WipeDbAsync();

        var stale = await SeedAsync(Session(
            "t1", "merging", Now.AddHours(1),
            updatedAt: Now.AddHours(-2)));      // 默认阈值 60 分钟 → stale
        var fresh = await SeedAsync(Session("t1", "merging", Now.AddHours(1)));

        await Uploads.CleanupExpiredAsync(CancellationToken.None);

        var staleEntity = await Db.UploadSessions.AsNoTracking()
            .SingleAsync(s => s.Id == stale.Id);
        Assert.Equal("failed", staleEntity.Status);
        Assert.NotNull(staleEntity.ErrorMessage);

        var freshEntity = await Db.UploadSessions.AsNoTracking()
            .SingleAsync(s => s.Id == fresh.Id);
        Assert.Equal("merging", freshEntity.Status);
    }

    // ── 4. pending_upload 孤儿元数据 ──

    private DocumentMetadataEntity Metadata(
        string tenant, string docId, string status, DateTimeOffset updatedAt)
        => new()
        {
            DocId = docId, Tenant = tenant, FileName = "a.bin",
            ContentType = "application/octet-stream", Size = 1000,
            ObjectKey = $"{tenant}/{docId}/v1/a.bin", Status = status,
            CreatedAt = Now, UpdatedAt = updatedAt,
        };

    [Fact]
    public async Task Cleanup_DeletesStalePendingUploadMetadata_AndObject()
    {
        await WipeDbAsync();
        var key = "t1/orphan/v1/a.bin";
        await Storage.PutAsync(key, new MemoryStream(new byte[100]), 100, "application/octet-stream");

        Db.DocumentMetadata.Add(Metadata(
            "t1", "orphan", "pending_upload", Now.AddHours(-3)));  // 默认阈值 120 分钟 → stale
        await Db.SaveChangesAsync();

        await Uploads.CleanupExpiredAsync(CancellationToken.None);

        Assert.False(await Db.DocumentMetadata.AnyAsync(m => m.ObjectKey == key));
        Assert.Null(Storage.GetBytes(key));
        Assert.True(Storage.DeleteCallCount > 0);
    }

    [Fact]
    public async Task Cleanup_OrphanObjectDeleteFailure_RetriesNextRound()
    {
        await WipeDbAsync();
        var key = "t1/orphan2/v1/a.bin";
        await Storage.PutAsync(key, new MemoryStream(new byte[100]), 100, "application/octet-stream");

        Db.DocumentMetadata.Add(Metadata(
            "t1", "orphan2", "pending_upload", Now.AddHours(-3)));
        await Db.SaveChangesAsync();

        // 删除失败 → 元数据保留（UpdatedAt 被刷新推迟下次扫描），对象仍在
        Storage.DeleteOverride = _ => Task.FromException<bool>(new IOException("S3 down"));

        await Uploads.CleanupExpiredAsync(CancellationToken.None);

        Assert.True(await Db.DocumentMetadata.AnyAsync(m => m.ObjectKey == key));
        Assert.NotNull(Storage.GetBytes(key));
    }

    // ── 5. delete_pending 重试 ──

    [Fact]
    public async Task Cleanup_RetriesDeletePendingMetadata_SuccessMarksDeleted()
    {
        await WipeDbAsync();
        var key = "t1/todelete/v1/a.bin";
        await Storage.PutAsync(key, new MemoryStream(new byte[100]), 100, "application/octet-stream");

        Db.DocumentMetadata.Add(Metadata(
            "t1", "todelete", "delete_pending", Now.AddHours(-3)));
        await Db.SaveChangesAsync();

        await Uploads.CleanupExpiredAsync(CancellationToken.None);

        var status = await Db.DocumentMetadata
            .Where(m => m.ObjectKey == key).Select(m => m.Status).SingleAsync();
        Assert.Equal("deleted", status);
        Assert.Null(Storage.GetBytes(key));
    }

    [Fact]
    public async Task Cleanup_RetriesDeletePendingMetadata_FailureKeepsPending()
    {
        await WipeDbAsync();
        var key = "t1/todelete2/v1/a.bin";
        await Storage.PutAsync(key, new MemoryStream(new byte[100]), 100, "application/octet-stream");

        Db.DocumentMetadata.Add(Metadata(
            "t1", "todelete2", "delete_pending", Now.AddHours(-3)));
        await Db.SaveChangesAsync();

        Storage.DeleteOverride = _ => Task.FromResult(false);   // S3 报告删除失败

        await Uploads.CleanupExpiredAsync(CancellationToken.None);

        var status = await Db.DocumentMetadata
            .Where(m => m.ObjectKey == key).Select(m => m.Status).SingleAsync();
        Assert.Equal("delete_pending", status);
        Assert.NotNull(Storage.GetBytes(key));
    }

    // ── 6. failed 会话 → cleaned ──

    [Fact]
    public async Task Cleanup_MarksStaleFailedSessionsAsCleaned_AndDeletesChunks()
    {
        await WipeDbAsync();

        var failed = await SeedAsync(
            Session("t1", "failed", Now.AddHours(1),
                updatedAt: Now.AddHours(-3)),       // stalePending 阈值 120 分钟 → stale
            withChunk: true);
        var fresh = await SeedAsync(Session(
            "t1", "failed", Now.AddHours(1)));      // 未 stale

        await Uploads.CleanupExpiredAsync(CancellationToken.None);

        Assert.Equal("cleaned", await Db.UploadSessions
            .Where(s => s.Id == failed.Id).Select(s => s.Status).SingleAsync());
        Assert.False(await Db.UploadChunks.AnyAsync(c => c.UploadId == failed.Id));
        Assert.Equal("failed", await Db.UploadSessions
            .Where(s => s.Id == fresh.Id).Select(s => s.Status).SingleAsync());
    }
}
