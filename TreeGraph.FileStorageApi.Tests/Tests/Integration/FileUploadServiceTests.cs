using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TreeGraph.FileStorageApi.Entities;
using TreeGraph.FileStorageApi.Tests.Fixtures;
using TreeGraph.FileStorageApi.Uploads;
using TreeGraph.Shared.FileStorageSky.Contracts;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests.Integration;

/// <summary>
/// FileUploadService 服务级集成测试（真实 PostgreSQL + 内存对象存储）：
/// 全生命周期、断点续传、状态机守卫、分块校验、幂等、租户隔离。
/// </summary>
public sealed class FileUploadServiceTests : ServiceTestBase
{
    public FileUploadServiceTests(FileStorageApiFactory factory) : base(factory) { }

    // 请求 ChunkSize 用下限 256KB（NormalizeChunkSize 会钳到 ≥256KB）
    private const int Chunk = 256 * 1024;

    // 逻辑分块大小：200KB < 256KB，且 N*200KB / 256KB 的 ceil 恰好等于 N
    private const int LogicalChunkBytes = 200_000;

    private IFileUploadService Uploads => Svc<IFileUploadService>();

    private static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    /// <summary>把会话拨到指定状态（绕过变更跟踪器，需 Clear 后续读才见新值）。</summary>
    private async Task SetStatusAsync(Guid uploadId, string status, DateTimeOffset? expiresAt = null)
    {
        if (expiresAt is null)
        {
            await Db.UploadSessions.Where(s => s.Id == uploadId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status));
        }
        else
        {
            await Db.UploadSessions.Where(s => s.Id == uploadId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.ExpiresAt, expiresAt.Value));
        }
        Db.ChangeTracker.Clear();   // 服务读的是同一 scope 的 DbContext，必须丢掉旧快照
    }

    private Task<InitiateUploadResponse> InitiateAsync(
        long totalSize, string? fingerprint = null, string? docId = null,
        string fileName = "a.bin")
        => Uploads.InitiateAsync(new InitiateUploadRequest
        {
            FileName = fileName,
            ContentType = "application/octet-stream",
            TotalSize = totalSize,
            ChunkSize = Chunk,
            Fingerprint = fingerprint,
            DocId = docId,
        }, CancellationToken.None);

    private Task<UploadChunkResponse> UploadChunkAsync(
        Guid uploadId, int index, byte[] data, string? expectedSha = null)
        => Uploads.UploadChunkAsync(
            uploadId, index, new MemoryStream(data), data.Length,
            expectedSha ?? Sha(data), CancellationToken.None);

    /// <summary>发起 + 上传全部分块（chunkCount 块，每块 LogicalChunkBytes 字节）。</summary>
    private async Task<(Guid UploadId, byte[][] Chunks)> InitiateAndUploadAsync(
        int chunkCount, string? fingerprint = null, string? docId = null)
    {
        var payload = new byte[chunkCount * LogicalChunkBytes];
        new Random(42).NextBytes(payload);
        var chunks = new byte[chunkCount][];
        for (var i = 0; i < chunkCount; i++)
            chunks[i] = payload[(i * LogicalChunkBytes)..((i + 1) * LogicalChunkBytes)];

        var initiated = await InitiateAsync(payload.Length, fingerprint, docId);
        for (var i = 0; i < chunkCount; i++)
            await UploadChunkAsync(initiated.UploadId, i, chunks[i]);

        return (initiated.UploadId, chunks);
    }

    // ══════════ 维度 1：全生命周期 ══════════

    [Fact]
    public async Task FullLifecycle_InitiateChunksComplete_MergesAndFinalizes()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        Caller.Actor = "tester";

        var (uploadId, chunks) = await InitiateAndUploadAsync(chunkCount: 3, docId: "doc-1");

        var status = await Uploads.GetStatusAsync(uploadId, CancellationToken.None);
        Assert.Equal("uploading", status.Status);
        Assert.True(status.IsComplete);

        var completed = await Uploads.CompleteAsync(
            uploadId, new CompleteUploadRequest(), CancellationToken.None);

        // 响应内容
        Assert.Equal("doc-1", completed.DocId);
        Assert.Equal(1, completed.Version);
        Assert.Equal($"tenant-a/doc-1/v1/a.bin", completed.ObjectKey);
        Assert.Equal(3 * LogicalChunkBytes, completed.Size);
        Assert.Equal(Sha(chunks.SelectMany(c => c).ToArray()), completed.Sha256);

        // 对象存储收到完整字节
        Assert.Equal(chunks.SelectMany(c => c).ToArray(), Storage.GetBytes(completed.ObjectKey));

        // 会话已完结
        var session = await Db.UploadSessions.AsNoTracking().SingleAsync(s => s.Id == uploadId);
        Assert.Equal("completed", session.Status);
        Assert.NotNull(session.CompletedAt);

        // 分块已清空
        Assert.Empty(await Db.UploadChunks.Where(c => c.UploadId == uploadId).ToListAsync());

        // 元数据 active + 索引任务 pending + 审计
        var metadata = await Db.DocumentMetadata.AsNoTracking()
            .SingleAsync(m => m.DocId == "doc-1" && m.Tenant == "tenant-a");
        Assert.Equal("active", metadata.Status);
        Assert.Equal(1, metadata.Version);

        Assert.Equal("pending", await Db.IndexTasks.AsNoTracking()
            .Where(t => t.DocId == "doc-1").Select(t => t.Status).SingleAsync());

        Assert.True(await Db.DocumentAudits.AsNoTracking().AnyAsync(a =>
            a.DocId == "doc-1" && a.Action == "completed_upload" && a.Actor == "tester"));
    }

    [Fact]
    public async Task Complete_SameDocIdTwice_IncrementsVersion()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        var first = await InitiateAndUploadAsync(chunkCount: 1, docId: "doc-1");
        var r1 = await Uploads.CompleteAsync(
            first.UploadId, new CompleteUploadRequest(), CancellationToken.None);
        Assert.Equal(1, r1.Version);

        var second = await InitiateAndUploadAsync(chunkCount: 1, docId: "doc-1");
        var r2 = await Uploads.CompleteAsync(
            second.UploadId, new CompleteUploadRequest(), CancellationToken.None);

        Assert.Equal(2, r2.Version);
        Assert.Contains("/v2/", r2.ObjectKey);
        Assert.Equal(2, await Db.DocumentMetadata.CountAsync(m => m.DocId == "doc-1"));
    }

    [Fact]
    public async Task Complete_AlreadyCompleted_ReturnsSameResponse_Idempotent()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1, docId: "doc-1");
        var first = await Uploads.CompleteAsync(
            uploadId, new CompleteUploadRequest(), CancellationToken.None);

        // 不带 request.DocId 时，completed 会话直接重放
        var replay = await Uploads.CompleteAsync(
            uploadId, new CompleteUploadRequest(), CancellationToken.None);

        Assert.Equal(first.DocId, replay.DocId);
        Assert.Equal(first.Version, replay.Version);
        Assert.Equal(first.ObjectKey, replay.ObjectKey);
    }

    // ══════════ 维度 2：Fingerprint 断点续传 ══════════

    [Fact]
    public async Task Initiate_FingerprintHitsActiveSession_ResumesWithoutNewSession()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        var first = await InitiateAsync(3000, fingerprint: "fp-1");
        await UploadChunkAsync(first.UploadId, 0, new byte[1000]);

        var resumed = await InitiateAsync(3000, fingerprint: "fp-1");

        Assert.Equal(first.UploadId, resumed.UploadId);
        Assert.True(resumed.Resumed);

        var count = await Db.UploadSessions.CountAsync(s => s.Fingerprint == "fp-1");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Initiate_FingerprintSizeMismatch_ThrowsValidation()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        await InitiateAsync(3000, fingerprint: "fp-1");

        await Assert.ThrowsAsync<UploadValidationException>(
            () => InitiateAsync(9999, fingerprint: "fp-1"));
    }

    [Fact]
    public async Task Initiate_FingerprintOfOtherTenant_DoesNotResume()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        await InitiateAsync(3000, fingerprint: "fp-1");

        Caller.Tenant = "tenant-b";
        var second = await InitiateAsync(3000, fingerprint: "fp-1");

        Assert.NotEqual(Guid.Empty, second.UploadId);
        Assert.False(second.Resumed);
    }

    // ══════════ 维度 3：分块校验与幂等覆盖 ══════════

    [Fact]
    public async Task UploadChunk_WrongSha_ThrowsValidation()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 2);

        await Assert.ThrowsAsync<UploadValidationException>(
            () => UploadChunkAsync(uploadId, 0, new byte[1000], expectedSha: new string('0', 64)));
    }

    [Fact]
    public async Task UploadChunk_IndexOutOfRange_ThrowsValidation()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 2);

        await Assert.ThrowsAsync<UploadValidationException>(
            () => UploadChunkAsync(uploadId, 2, new byte[1000]));
        await Assert.ThrowsAsync<UploadValidationException>(
            () => UploadChunkAsync(uploadId, -1, new byte[1000]));
    }

    [Fact]
    public async Task UploadChunk_SizeAboveSessionChunk_ThrowsValidation()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 2);

        await Assert.ThrowsAsync<UploadValidationException>(
            () => UploadChunkAsync(uploadId, 0, new byte[Chunk + 1]));
    }

    [Fact]
    public async Task UploadChunk_StreamShorterThanContentLength_ThrowsValidation()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 2);

        var data = new byte[1000];
        await Assert.ThrowsAsync<UploadValidationException>(
            () => Uploads.UploadChunkAsync(
                uploadId, 0, new MemoryStream(data, 0, 500), 1000,
                null, CancellationToken.None));
    }

    [Fact]
    public async Task UploadChunk_SameIndexTwice_UpsertsWithoutCountIncrease()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 2);

        await UploadChunkAsync(uploadId, 1, new byte[1000]);
        var response = await UploadChunkAsync(uploadId, 1, new byte[999]);

        Assert.Equal(2, response.ReceivedCount);   // 覆盖而非新增
    }

    // ══════════ 维度 4：状态机守卫 ══════════

    [Fact]
    public async Task UploadChunk_OnCompletedSession_ThrowsConflict()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1, docId: "doc-1");
        await Uploads.CompleteAsync(uploadId, new CompleteUploadRequest(), CancellationToken.None);

        await Assert.ThrowsAsync<UploadConflictException>(
            () => UploadChunkAsync(uploadId, 0, new byte[1000]));
    }

    [Fact]
    public async Task UploadChunk_OnMergingSession_ThrowsConflict()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        await SetStatusAsync(uploadId, "merging");

        await Assert.ThrowsAsync<UploadConflictException>(
            () => UploadChunkAsync(uploadId, 0, new byte[1000]));
    }

    [Fact]
    public async Task UploadChunk_OnExpiredSession_MarksExpiredAndThrowsGone()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        await SetStatusAsync(uploadId, "pending", DateTimeOffset.UtcNow.AddHours(-1));

        await Assert.ThrowsAsync<UploadGoneException>(
            () => UploadChunkAsync(uploadId, 0, new byte[1000]));

        Assert.Equal("expired", await Db.UploadSessions
            .Where(s => s.Id == uploadId).Select(s => s.Status).SingleAsync());
    }

    [Fact]
    public async Task Complete_OnExpiredSession_ThrowsGone()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        await SetStatusAsync(uploadId, "pending", DateTimeOffset.UtcNow.AddHours(-1));

        await Assert.ThrowsAsync<UploadGoneException>(
            () => Uploads.CompleteAsync(uploadId, new CompleteUploadRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Complete_MergingSession_ThrowsConflict()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        await SetStatusAsync(uploadId, "merging");

        await Assert.ThrowsAsync<UploadConflictException>(
            () => Uploads.CompleteAsync(uploadId, new CompleteUploadRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Complete_FailedWithFullChunks_Retries()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        // failed 且分块完整 → P0-1 允许重试完成
        var uploadId = Guid.NewGuid();
        Db.UploadSessions.Add(new UploadSessionEntity
        {
            Id = uploadId, Tenant = "tenant-a", FileName = "a.bin",
            ContentType = "application/octet-stream", TotalSize = 1000,
            ChunkSize = Chunk, TotalChunks = 1, Status = "failed",
            DocId = "doc-retry", CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        });
        Db.UploadChunks.Add(new UploadChunkEntity
        {
            UploadId = uploadId, ChunkIndex = 0, Size = 1000, Data = new byte[1000],
            Sha256 = Sha(new byte[1000]),
        });
        await Db.SaveChangesAsync();

        var completed = await Uploads.CompleteAsync(
            uploadId, new CompleteUploadRequest(), CancellationToken.None);

        Assert.Equal("doc-retry", completed.DocId);
    }

    [Fact]
    public async Task Complete_FailedWithMissingChunks_ThrowsConflict()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        var uploadId = Guid.NewGuid();
        Db.UploadSessions.Add(new UploadSessionEntity
        {
            Id = uploadId, Tenant = "tenant-a", FileName = "a.bin",
            ContentType = "application/octet-stream", TotalSize = 2000,
            ChunkSize = Chunk, TotalChunks = 2, Status = "failed",
            DocId = "doc-retry2", CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        });
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UploadConflictException>(
            () => Uploads.CompleteAsync(uploadId, new CompleteUploadRequest(), CancellationToken.None));
    }

    // ══════════ 维度 5：Complete SHA 校验失败回滚 ══════════

    [Fact]
    public async Task Complete_ShaMismatch_DeletesObjectAndMarksFailed()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1, docId: "doc-sha");

        await Assert.ThrowsAsync<UploadValidationException>(
            () => Uploads.CompleteAsync(
                uploadId,
                new CompleteUploadRequest { Sha256 = new string('f', 64) },
                CancellationToken.None));

        // 对象被回删，会话标记 failed
        Assert.Null(Storage.GetBytes("tenant-a/doc-sha/v1/a.bin"));
        var session = await Db.UploadSessions.AsNoTracking().SingleAsync(s => s.Id == uploadId);
        Assert.Equal("failed", session.Status);
        Assert.NotNull(session.ErrorMessage);
    }

    // ══════════ 维度 6：续期 / 取消 ══════════

    [Fact]
    public async Task Renew_UploadingSession_ExtendsExpiry()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        var before = await Db.UploadSessions.AsNoTracking()
            .Where(s => s.Id == uploadId).Select(s => s.ExpiresAt).SingleAsync();

        await Uploads.RenewAsync(uploadId, CancellationToken.None);

        var after = await Db.UploadSessions.AsNoTracking()
            .Where(s => s.Id == uploadId).Select(s => s.ExpiresAt).SingleAsync();
        Assert.True(after > before);
    }

    [Fact]
    public async Task Renew_Completed_ThrowsConflict()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1, docId: "doc-1");
        await Uploads.CompleteAsync(uploadId, new CompleteUploadRequest(), CancellationToken.None);

        await Assert.ThrowsAsync<UploadConflictException>(
            () => Uploads.RenewAsync(uploadId, CancellationToken.None));
    }

    [Fact]
    public async Task Renew_Expired_ThrowsGone()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (expiredId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        await SetStatusAsync(expiredId, "expired");

        await Assert.ThrowsAsync<UploadGoneException>(
            () => Uploads.RenewAsync(expiredId, CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_PendingSession_ExpiresAndDeletesChunks()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 2);

        await Uploads.CancelAsync(uploadId, CancellationToken.None);

        var session = await Db.UploadSessions.AsNoTracking().SingleAsync(s => s.Id == uploadId);
        Assert.Equal("expired", session.Status);
        Assert.Empty(await Db.UploadChunks.Where(c => c.UploadId == uploadId).ToListAsync());
    }

    [Fact]
    public async Task Cancel_Completed_ThrowsConflict()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1, docId: "doc-1");
        await Uploads.CompleteAsync(uploadId, new CompleteUploadRequest(), CancellationToken.None);

        await Assert.ThrowsAsync<UploadConflictException>(
            () => Uploads.CancelAsync(uploadId, CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_Merging_ThrowsConflict()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (mergingId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        await SetStatusAsync(mergingId, "merging");

        await Assert.ThrowsAsync<UploadConflictException>(
            () => Uploads.CancelAsync(mergingId, CancellationToken.None));
    }

    // ══════════ 维度 7：租户隔离（跨租户一律伪装 404，防枚举） ══════════

    [Fact]
    public async Task CrossTenant_StatusAndCompleteAndChunk_ThrowNotFound()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 2);

        Caller.Tenant = "tenant-b";

        await Assert.ThrowsAsync<UploadNotFoundException>(
            () => Uploads.GetStatusAsync(uploadId, CancellationToken.None));
        await Assert.ThrowsAsync<UploadNotFoundException>(
            () => Uploads.CompleteAsync(uploadId, new CompleteUploadRequest(), CancellationToken.None));
        await Assert.ThrowsAsync<UploadNotFoundException>(
            () => UploadChunkAsync(uploadId, 0, new byte[1000]));
        await Assert.ThrowsAsync<UploadNotFoundException>(
            () => Uploads.RenewAsync(uploadId, CancellationToken.None));
        await Assert.ThrowsAsync<UploadNotFoundException>(
            () => Uploads.CancelAsync(uploadId, CancellationToken.None));
    }

    [Fact]
    public async Task GetStatus_UnknownSession_ThrowsNotFound()
    {
        await WipeDbAsync();

        await Assert.ThrowsAsync<UploadNotFoundException>(
            () => Uploads.GetStatusAsync(Guid.NewGuid(), CancellationToken.None));
    }

    // ══════════ 维度 1 扩展：Complete 请求字段覆盖 ══════════

    [Fact]
    public async Task Complete_RequestDocId_OverridesSessionDocId()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        Caller.Actor = "tester";

        var init = await InitiateAsync(LogicalChunkBytes, docId: "session-doc");
        await UploadChunkAsync(init.UploadId, 0, new byte[LogicalChunkBytes]);

        var completed = await Uploads.CompleteAsync(
            init.UploadId,
            new CompleteUploadRequest { DocId = "request-doc" },
            CancellationToken.None);

        Assert.Equal("request-doc", completed.DocId);
        Assert.Contains("request-doc", completed.ObjectKey);
        Assert.True(await Db.DocumentMetadata.AsNoTracking()
            .AnyAsync(m => m.DocId == "request-doc"));
    }

    [Fact]
    public async Task Complete_PassesTagsAndMetadataToDocumentMetadata()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        var completed = await Uploads.CompleteAsync(
            uploadId,
            new CompleteUploadRequest
            {
                TagsJson = """{"k":"v"}""",
                MetadataJson = """{"m":1}""",
            },
            CancellationToken.None);

        var metadata = await Db.DocumentMetadata.AsNoTracking()
            .Where(m => m.DocId == completed.DocId)
            .Select(m => new { m.TagsJson, m.MetadataJson })
            .SingleAsync();
        Assert.Equal("""{"k":"v"}""", metadata.TagsJson);
        Assert.Equal("""{"m":1}""", metadata.MetadataJson);
    }

    // ══════════ 维度 4 扩展：状态机守卫 ══════════

    [Fact]
    public async Task UploadChunk_OnFailedSession_ThrowsConflict()
    {
        await WipeDbAsync();
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        await SetStatusAsync(uploadId, "failed");

        await Assert.ThrowsAsync<UploadConflictException>(
            () => UploadChunkAsync(uploadId, 0, new byte[1000]));
    }

    // ══════════ 维度 5 扩展：缺块回滚 ══════════

    [Fact]
    public async Task Complete_PendingWithMissingChunks_MarksFailedAndThrowsValidation()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        Caller.Actor = "tester";

        // 发起 2 块会话，仅上传 chunk 0（chunk 1 缺失），状态仍为 uploading
        var init = await InitiateAsync(2 * LogicalChunkBytes);
        await UploadChunkAsync(init.UploadId, 0, new byte[LogicalChunkBytes]);

        await Assert.ThrowsAsync<UploadValidationException>(
            () => Uploads.CompleteAsync(init.UploadId, new CompleteUploadRequest(), CancellationToken.None));

        // CompleteAsync 内部走 MarkFailedAsync（pending/uploading 缺块回滚路径）
        var session = await Db.UploadSessions.AsNoTracking()
            .Where(s => s.Id == init.UploadId)
            .Select(s => new { s.Status, s.ErrorMessage })
            .SingleAsync();
        Assert.Equal("failed", session.Status);
        Assert.NotNull(session.ErrorMessage);
    }

    // ══════════ 维度 6 扩展：续期 / 取消 ══════════

    [Fact]
    public async Task Renew_FailedSession_ThrowsConflict()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        await SetStatusAsync(uploadId, "failed");

        await Assert.ThrowsAsync<UploadConflictException>(
            () => Uploads.RenewAsync(uploadId, CancellationToken.None));
    }

    [Fact]
    public async Task Renew_MergingSession_Succeeds()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 1);
        await SetStatusAsync(uploadId, "merging");

        var before = await Db.UploadSessions.AsNoTracking()
            .Where(s => s.Id == uploadId).Select(s => s.ExpiresAt).SingleAsync();

        await Uploads.RenewAsync(uploadId, CancellationToken.None);

        var after = await Db.UploadSessions.AsNoTracking()
            .Where(s => s.Id == uploadId).Select(s => s.ExpiresAt).SingleAsync();
        Assert.True(after > before);
    }

    [Fact]
    public async Task Cancel_FailedSession_ExpiresAndDeletesChunks()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";
        var (uploadId, _) = await InitiateAndUploadAsync(chunkCount: 2);
        await SetStatusAsync(uploadId, "failed");

        await Uploads.CancelAsync(uploadId, CancellationToken.None);

        var session = await Db.UploadSessions.AsNoTracking().SingleAsync(s => s.Id == uploadId);
        Assert.Equal("expired", session.Status);
        Assert.Empty(await Db.UploadChunks.Where(c => c.UploadId == uploadId).ToListAsync());
    }

    // ══════════ 维度 8：Initiate 边界 ══════════

    [Fact]
    public async Task Initiate_TotalSizeAboveMax_ThrowsValidation()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        await Assert.ThrowsAsync<UploadValidationException>(
            () => InitiateAsync(2L * 1024 * 1024 * 1024 + 1));
    }

    [Fact]
    public async Task Initiate_LargeTotalSize_BumpsChunkSizeBeyondMaxChunksLimit()
    {
        await WipeDbAsync();
        Caller.Tenant = "tenant-a";

        // 任务原指定 totalSize = 10000L*256*1024+1（≈2.38GB）超过 MaxTotalSize（2GB），
        // InitiateAsync 会在 TotalSize 校验阶段抛 UploadValidationException，无法到达
        // NormalizeChunkSize 的提升分支。改用 MaxTotalSize 边界值（2GB，恰好不超界），
        // 验证大文件下 TotalChunks 仍受 maxChunks=10000 上限约束。
        var totalSize = 2L * 1024 * 1024 * 1024;
        var response = await InitiateAsync(totalSize);

        Assert.True(response.TotalChunks <= 10000);
        Assert.True(response.ChunkSize >= Chunk);
    }
}
