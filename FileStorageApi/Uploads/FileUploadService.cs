using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FileStorageApi.Data;
using FileStorageApi.Entities;
using FileStorageApi.Security;
using FileStorageApi.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FileStorageApi.Uploads;

public sealed class FileUploadService : IFileUploadService
{
    private const int DefaultChunkSize = 4 * 1024 * 1024;
    private const int MinChunkSize     = 256 * 1024;
    private const int MaxChunkSize     = 16 * 1024 * 1024;
    private const long MaxTotalSize    = 2L * 1024 * 1024 * 1024;
    private const int MaxVersionRetries = 5;
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);

    // ★ S3 合并阶段的独立超时（与客户端 HTTP 请求的 CT 解耦）
    private static readonly TimeSpan MergeTimeout = TimeSpan.FromMinutes(10);

    // ★ 分片删除批次：每批 20 个（1 GB 文件 = 127 片，约 7 批）
    //   避免单条 DELETE 在 30s+ 上超时。
    private const int ChunkDeleteBatchSize = 20;

    private readonly FileStorageContext _db;
    private readonly IObjectStorage _storage;
    private readonly ICallerContext _caller;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<UploadCleanupOptions> _cleanupOptions;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(
        FileStorageContext db,
        IObjectStorage storage,
        ICallerContext caller,
        IServiceScopeFactory scopeFactory,
        IOptions<UploadCleanupOptions> cleanupOptions,
        ILogger<FileUploadService> logger)
    {
        _db             = db;
        _storage        = storage;
        _caller         = caller;
        _scopeFactory   = scopeFactory;
        _cleanupOptions = cleanupOptions;
        _logger         = logger;
    }

    // ───────────────────────────────────────────────────────────
    // 1. 初始化 / 恢复会话
    // ───────────────────────────────────────────────────────────
    public async Task<InitiateUploadResponse> InitiateAsync(
        InitiateUploadRequest request, CancellationToken ct)
    {
        if (request.TotalSize <= 0 || request.TotalSize > MaxTotalSize)
            throw new UploadValidationException(
                $"文件大小必须在 1 字节至 {MaxTotalSize} 字节之间。");

        // ★ P0-2：Fingerprint 命中已有活跃会话 → 直接返回，客户端续传
        if (!string.IsNullOrWhiteSpace(request.Fingerprint))
        {
            var existing = await _db.UploadSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Tenant == _caller.Tenant &&
                    s.Fingerprint == request.Fingerprint &&
                    (s.Status == "pending" ||
                     s.Status == "uploading" ||
                     s.Status == "merging") &&
                    s.ExpiresAt > DateTimeOffset.UtcNow, ct);

            if (existing is not null)
            {
                if (existing.TotalSize != request.TotalSize)
                    throw new UploadValidationException(
                        $"Fingerprint 命中的既有会话文件大小（{existing.TotalSize}）" +
                        $"与本次请求（{request.TotalSize}）不一致。");

                _logger.LogInformation(
                    "Fingerprint 命中活跃会话 {UploadId}（状态 {Status}），复用。",
                    existing.Id, existing.Status);

                return InitiateUploadResponse.FromSession(existing, resumed: true);
            }
        }

        var chunkSize = NormalizeChunkSize(request.ChunkSize, request.TotalSize);
        var totalChunks = (int)Math.Ceiling((double)request.TotalSize / chunkSize);

        var session = new UploadSessionEntity
        {
            Tenant      = _caller.Tenant,
            FileName    = request.FileName,
            ContentType = request.ContentType,
            TotalSize   = request.TotalSize,
            ChunkSize   = chunkSize,
            TotalChunks = totalChunks,
            Status      = "pending",
            DocId       = request.DocId,
            Fingerprint = string.IsNullOrWhiteSpace(request.Fingerprint)
                            ? null : request.Fingerprint,
            CreatedAt   = DateTimeOffset.UtcNow,
            UpdatedAt   = DateTimeOffset.UtcNow,
            ExpiresAt   = DateTimeOffset.UtcNow.Add(SessionTtl),
        };

        _db.UploadSessions.Add(session);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // 并发 initiate 同 Fingerprint：重新读既有会话
            _db.Entry(session).State = EntityState.Detached;
            var existing = await _db.UploadSessions
                .AsNoTracking()
                .FirstAsync(s => s.Tenant == _caller.Tenant &&
                                 s.Fingerprint == session.Fingerprint &&
                                 (s.Status == "pending" ||
                                  s.Status == "uploading" ||
                                  s.Status == "merging"), ct);
            return InitiateUploadResponse.FromSession(existing, resumed: true);
        }

        return InitiateUploadResponse.FromSession(session, resumed: false);
    }

    // ───────────────────────────────────────────────────────────
    // 2. 上传分块
    // ───────────────────────────────────────────────────────────
    public async Task<UploadChunkResponse> UploadChunkAsync(
        Guid uploadId, int chunkIndex, Stream data, long contentLength,
        string? expectedSha256, CancellationToken ct)
    {
        var session = await GetActiveSessionAsync(uploadId, ct);

        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
            throw new UploadValidationException(
                $"分块索引必须在 [0, {session.TotalChunks}) 范围内。");

        if (contentLength <= 0 || contentLength > session.ChunkSize)
            throw new UploadValidationException(
                $"分块大小必须在 1 至 {session.ChunkSize} 字节之间。");

        var buffer = new byte[contentLength];
        var read = 0;
        while (read < contentLength)
        {
            var n = await data.ReadAsync(
                buffer.AsMemory(read, (int)contentLength - read), ct);
            if (n == 0) break;
            read += n;
        }
        if (read != contentLength)
            throw new UploadValidationException(
                $"分块读取不完整：期望 {contentLength}，实际 {read}。");

        var computed = Convert.ToHexString(SHA256.HashData(buffer));

        // ★ P1-2：客户端声明的分块 SHA256 校验
        if (!string.IsNullOrWhiteSpace(expectedSha256) &&
            !string.Equals(computed, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new UploadValidationException(
                $"分块 {chunkIndex} SHA256 不匹配：期望 {expectedSha256}，实际 {computed}。");
        }

        await _db.UpsertChunkAsync(uploadId, chunkIndex, buffer, computed, ct);

        await _db.UploadSessions
            .Where(s => s.Id == uploadId && s.Status == "pending")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "uploading")
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);

        var receivedCount = await _db.UploadChunks
            .CountAsync(c => c.UploadId == uploadId, ct);

        return new UploadChunkResponse(
            uploadId, chunkIndex, receivedCount, session.TotalChunks);
    }

    // ───────────────────────────────────────────────────────────
    // 3. 查询状态
    // ───────────────────────────────────────────────────────────
    public async Task<UploadStatusResponse> GetStatusAsync(
        Guid uploadId, CancellationToken ct)
    {
        var session = await _db.UploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == uploadId, ct)
            ?? throw new UploadNotFoundException($"上传会话不存在：{uploadId}");

        if (session.Tenant != _caller.Tenant)
            throw new UploadNotFoundException($"上传会话不存在：{uploadId}");

        var received = await _db.UploadChunks
            .Where(c => c.UploadId == uploadId)
            .Select(c => c.ChunkIndex)
            .OrderBy(i => i)
            .ToListAsync(ct);

        return UploadStatusResponse.FromSession(session, received);
    }

    // ───────────────────────────────────────────────────────────
    // 4. 完成（支持 failed 重试）
    // ───────────────────────────────────────────────────────────
    public async Task<CompleteUploadResponse> CompleteAsync(
        Guid uploadId, CompleteUploadRequest request, CancellationToken ct)
    {
        // ── 4.1 幂等 / 状态检查 ──
        var pre = await _db.UploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == uploadId, ct)
            ?? throw new UploadNotFoundException($"上传会话不存在：{uploadId}");

        if (pre.Tenant != _caller.Tenant)
            throw new UploadNotFoundException($"上传会话不存在：{uploadId}");

        if (pre.Status == "completed")
        {
            return new CompleteUploadResponse(
                pre.Id, pre.DocId!, pre.Version ?? 1, pre.ObjectKey!,
                pre.TotalSize, pre.Sha256);
        }
        if (pre.Status == "merging")
            throw new UploadConflictException("上传正在合并中，请稍后查询状态。");
        if (pre.Status == "expired")
            throw new UploadGoneException("上传会话已过期。");
        if (pre.ExpiresAt < DateTimeOffset.UtcNow)
            throw new UploadGoneException("上传会话已过期。");

        // ★ P0-1：failed 允许在分块完整时重试
        var allowedFromStates = new List<string> { "pending", "uploading" };
        if (pre.Status == "failed")
        {
            var present = await _db.UploadChunks
                .CountAsync(c => c.UploadId == uploadId, ct);
            if (present != pre.TotalChunks)
                throw new UploadConflictException(
                    $"会话状态为 'failed' 且分块不完整（{present}/{pre.TotalChunks}），" +
                    $"请用相同 Fingerprint 重新 initiate。");
            allowedFromStates.Add("failed");
        }

        // ── 4.2 原子抢占 merging ──
        var claimed = await _db.UploadSessions
            .Where(s => s.Id == uploadId && allowedFromStates.Contains(s.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "merging")
                .SetProperty(x => x.ErrorMessage, (string?)null)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);

        if (claimed == 0)
            throw new UploadConflictException("上传状态已变更，请查询最新状态后重试。");

        var session = await _db.UploadSessions
            .FirstAsync(s => s.Id == uploadId, ct);

        // ── 4.3 校验分块完整性 ──
        var chunkIndexes = await _db.UploadChunks
            .Where(c => c.UploadId == uploadId)
            .Select(c => c.ChunkIndex)
            .OrderBy(i => i)
            .ToListAsync(ct);

        if (chunkIndexes.Count != session.TotalChunks)
        {
            var missing = Enumerable.Range(0, session.TotalChunks)
                .Except(chunkIndexes).Take(20).ToList();
            await MarkFailedAsync(uploadId,
                $"分块不完整：已收 {chunkIndexes.Count}/{session.TotalChunks}，缺失：{string.Join(",", missing)}");
            throw new UploadValidationException(
                $"分块不完整：已收 {chunkIndexes.Count}/{session.TotalChunks}");
        }

        var docId = request.DocId ?? session.DocId ?? Guid.NewGuid().ToString("N");

        // ── 4.5 元数据 pending_upload + 乐观版本重试 ──
        DocumentMetadataEntity metadata = null!;
        string objectKey = string.Empty;
        int version = 0;

        for (int attempt = 0; ; attempt++)
        {
            version = await NextVersionAsync(session.Tenant, docId, ct);
            objectKey = BuildObjectKey(session.Tenant, docId, version, session.FileName);

            metadata = new DocumentMetadataEntity
            {
                Tenant       = session.Tenant,
                DocId        = docId,
                Version      = version,
                FileName     = session.FileName,
                ContentType  = session.ContentType,
                Size         = session.TotalSize,
                ObjectKey    = objectKey,
                Status       = "pending_upload",
                TagsJson     = request.TagsJson,
                MetadataJson = request.MetadataJson,
                CreatedAt    = DateTimeOffset.UtcNow,
                UpdatedAt    = DateTimeOffset.UtcNow,
            };
            _db.DocumentMetadata.Add(metadata);

            try { await _db.SaveChangesAsync(ct); break; }
            catch (DbUpdateException ex) when (
                IsUniqueViolation(ex) && attempt < MaxVersionRetries - 1)
            {
                _db.Entry(metadata).State = EntityState.Detached;
                _logger.LogWarning(
                    "版本冲突（docId={DocId}, version={Version}），第 {Attempt} 次重试",
                    docId, version, attempt + 1);
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), ct);
            }
        }

        // ★ 合并阶段使用独立 CT，与客户端请求解耦。
        using var mergeCts = new CancellationTokenSource(MergeTimeout);
        var mergeCt = mergeCts.Token;

        try
        {
            // ── 4.6 单遍：ChunkedReadStream → HashingReadStream → S3 ──
            var uploadIdLocal = uploadId;
            var totalChunks = session.TotalChunks;

            await using var chunked = new ChunkedReadStream(
                session.TotalSize, totalChunks,
                async (index, innerCt) =>
                {
                    var chunk = await _db.UploadChunks
                        .AsNoTracking()
                        .Where(c => c.UploadId == uploadIdLocal && c.ChunkIndex == index)
                        .Select(c => c.Data)
                        .FirstOrDefaultAsync(innerCt);
                    return chunk;
                });

            await using var hashing = new HashingReadStream(chunked);

            await _storage.PutAsync(
                objectKey, hashing, session.TotalSize, session.ContentType, mergeCt);

            if (hashing.Hash is null)
            {
                var drain = new byte[8192];
                while (await hashing.ReadAsync(drain, mergeCt) > 0) { }
            }

            var computed = hashing.Hash
                ?? throw new UploadValidationException("未能计算 SHA256（流未读到 EOF）。");

            if (!string.IsNullOrWhiteSpace(request.Sha256) &&
                !string.Equals(computed, request.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                try { await _storage.DeleteAsync(objectKey, CancellationToken.None); }
                catch (Exception ex) { _logger.LogWarning(ex, "回删校验失败对象：{Key}", objectKey); }

                await MarkFailedAsync(uploadId,
                    $"SHA256 校验失败：期望 {request.Sha256}，实际 {computed}。");
                throw new UploadValidationException("SHA256 校验失败。");
            }

            session.Sha256 = request.Sha256 ?? computed;

            // ── 4.7 第二阶段事务 ──
            await using var tx = await _db.Database.BeginTransactionAsync(mergeCt);

            metadata.Status    = "active";
            metadata.Sha256    = session.Sha256;
            metadata.UpdatedAt = DateTimeOffset.UtcNow;

            _db.DocumentAudits.Add(new DocumentAuditEntity
            {
                DocId       = docId,
                Tenant      = session.Tenant,
                Action      = "completed_upload",
                Actor       = _caller.Actor,
                DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    uploadId  = session.Id,
                    size      = session.TotalSize,
                    sha256    = session.Sha256,
                    objectKey,
                }),
            });

            _db.IndexTasks.Add(new IndexTaskEntity
            {
                DocId  = docId,
                Tenant = session.Tenant,
                Status = "pending",
            });

            session.Status      = "completed";
            session.DocId       = docId;
            session.Version     = version;
            session.ObjectKey   = objectKey;
            session.CompletedAt = DateTimeOffset.UtcNow;
            session.UpdatedAt   = DateTimeOffset.UtcNow;

            // ★ 分批删除分片，避免 1GB+ 数据触发单条 DELETE 30s 超时。
            //   每批 20 个（≈160 MB），约 7 批删除 1 GB。
            await DeleteChunksInBatchesAsync(uploadId, mergeCt);

            await _db.SaveChangesAsync(mergeCt);
            await tx.CommitAsync(mergeCt);

            _logger.LogInformation(
                "上传完成 {UploadId}：docId={DocId}, version={Version}, key={Key}",
                session.Id, docId, version, objectKey);

            return new CompleteUploadResponse(
                session.Id, docId, version, objectKey,
                session.TotalSize, session.Sha256);
        }
        catch (UploadValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(uploadId, ex.Message);
            _logger.LogError(ex, "合并上传失败：{UploadId}", uploadId);
            throw;
        }
    }

    // ───────────────────────────────────────────────────────────
    // 5. 续期
    // ───────────────────────────────────────────────────────────
    public async Task<bool> RenewAsync(Guid uploadId, CancellationToken ct)
    {
        var session = await _db.UploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == uploadId, ct);
        if (session is null || session.Tenant != _caller.Tenant) return false;
        if (session.Status is "completed" or "failed" or "expired") return false;

        var newExpiry = DateTimeOffset.UtcNow.Add(SessionTtl);
        var updated = await _db.UploadSessions
            .Where(s => s.Id == uploadId &&
                        (s.Status == "pending" || s.Status == "uploading" || s.Status == "merging"))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ExpiresAt, newExpiry)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);

        return updated > 0;
    }

    // ───────────────────────────────────────────────────────────
    // 6. 取消
    // ───────────────────────────────────────────────────────────
    public async Task<bool> CancelAsync(Guid uploadId, CancellationToken ct)
    {
        var session = await _db.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == uploadId, ct);
        if (session is null || session.Tenant != _caller.Tenant) return false;
        if (session.Status is "completed" or "merging") return false;

        // ★ 分批删除，与 CompleteAsync 一致
        await DeleteChunksInBatchesAsync(uploadId, ct);

        session.Status = "expired";
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ───────────────────────────────────────────────────────────
    // 7. 清理
    // ───────────────────────────────────────────────────────────
    public async Task<int> CleanupExpiredAsync(CancellationToken ct)
    {
        var opts = _cleanupOptions.Value;
        var now = DateTimeOffset.UtcNow;
        var batchSize = opts.EffectiveBatchSize;
        var removed = 0;

        // 7.1 过期会话（pending/uploading/failed/expired）
        while (true)
        {
            var batch = await _db.UploadSessions
                .Where(s => s.ExpiresAt < now &&
                            s.Status != "completed" &&
                            s.Status != "merging")
                .OrderBy(s => s.ExpiresAt)
                .Select(s => s.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            // ★ 每个会话内部再分批删除
            foreach (var id in batch)
                await DeleteChunksInBatchesAsync(id, ct);

            await _db.UploadSessions
                .Where(s => batch.Contains(s.Id))
                .ExecuteDeleteAsync(ct);

            removed += batch.Count;
            if (batch.Count < batchSize) break;
        }

        // ★ 7.1.1 completed 会话保留期清理
        var completedRetention = opts.EffectiveCompletedRetention;
        if (completedRetention > TimeSpan.Zero)
        {
            var completedBefore = now - completedRetention;

            while (true)
            {
                var batch = await _db.UploadSessions
                    .Where(s => s.Status == "completed" &&
                                s.CompletedAt != null &&
                                s.CompletedAt < completedBefore)
                    .OrderBy(s => s.CompletedAt)
                    .Select(s => s.Id)
                    .Take(batchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0) break;

                await _db.UploadSessions
                    .Where(s => batch.Contains(s.Id))
                    .ExecuteDeleteAsync(ct);

                if (batch.Count < batchSize) break;
            }
        }

        // 7.2 stale merging
        var staleMergingBefore = now - opts.EffectiveStaleMerging;
        await _db.UploadSessions
            .Where(s => s.Status == "merging" && s.UpdatedAt < staleMergingBefore)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "failed")
                .SetProperty(x => x.ErrorMessage, "合并超时，已由清理器标记失败")
                .SetProperty(x => x.UpdatedAt, now), ct);

        var stalePendingBefore = now - opts.EffectiveStalePending;

        // ★ 7.3 stale pending_upload：S3 删除失败时保留元数据，延后重试
        while (true)
        {
            var orphans = await _db.DocumentMetadata
                .Where(m => m.Status == "pending_upload" &&
                            m.UpdatedAt < stalePendingBefore)
                .OrderBy(m => m.UpdatedAt)
                .Take(batchSize)
                .ToListAsync(ct);

            if (orphans.Count == 0) break;

            var failedKeys = new ConcurrentBag<string>();

            await Parallel.ForEachAsync(
                orphans,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = opts.EffectiveOrphanDeleteConcurrency,
                    CancellationToken = ct,
                },
                async (orphan, innerCt) =>
                {
                    try
                    {
                        await _storage.DeleteAsync(orphan.ObjectKey, innerCt);
                        _logger.LogWarning("清理孤儿对象：{Key}", orphan.ObjectKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "清理孤儿对象失败：{Key}", orphan.ObjectKey);
                        failedKeys.Add(orphan.ObjectKey);
                    }
                });

            var failedSet = new HashSet<string>(failedKeys, StringComparer.Ordinal);
            var toDelete = new List<DocumentMetadataEntity>();

            foreach (var orphan in orphans)
            {
                if (failedSet.Contains(orphan.ObjectKey))
                {
                    orphan.UpdatedAt = now;
                }
                else
                {
                    toDelete.Add(orphan);
                }
            }

            if (toDelete.Count > 0)
                _db.DocumentMetadata.RemoveRange(toDelete);

            await _db.SaveChangesAsync(ct);

            if (orphans.Count < batchSize) break;
        }

        // 7.4 delete_pending 恢复
        while (true)
        {
            var pendings = await _db.DocumentMetadata
                .Where(m => m.Status == "delete_pending" &&
                            m.UpdatedAt < stalePendingBefore)
                .OrderBy(m => m.UpdatedAt)
                .Take(batchSize)
                .ToListAsync(ct);

            if (pendings.Count == 0) break;

            foreach (var p in pendings)
            {
                bool ok = false;
                try { ok = await _storage.DeleteAsync(p.ObjectKey, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "重试删除失败：{Key}", p.ObjectKey); }

                p.Status = ok ? "deleted" : "delete_pending";
                p.UpdatedAt = now;
            }
            await _db.SaveChangesAsync(ct);

            if (pendings.Count < batchSize) break;
        }

        // ★ 7.5 stale failed 分块清理
        while (true)
        {
            var failedIds = await _db.UploadSessions
                .Where(s => s.Status == "failed" && s.UpdatedAt < stalePendingBefore)
                .OrderBy(s => s.UpdatedAt)
                .Select(s => s.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            if (failedIds.Count == 0) break;

            foreach (var id in failedIds)
                await DeleteChunksInBatchesAsync(id, ct);

            await _db.UploadSessions
                .Where(s => failedIds.Contains(s.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "cleaned")
                    .SetProperty(x => x.UpdatedAt, now), ct);

            if (failedIds.Count < batchSize) break;
        }

        return removed;
    }

    // ───────────────────────────────────────────────────────────
    // 私有辅助
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// ★ 分批删除指定会话的全部分片。
    ///
    /// 为什么要分批：
    ///   1 GB 文件 = 127 个 8 MB 分片，单条 DELETE 涉及约 1 GB 的 bytea，
    ///   会触发 Npgsql 30s 命令超时（即使调高到 300s 也可能在更大文件上超）。
    ///   每批 20 片（≈160 MB），单条 DELETE 通常在数秒内完成。
    ///
    /// 用原始 SQL 是因为 EF Core 的 ExecuteDeleteAsync 不支持 Take/Limit。
    /// </summary>
    private async Task DeleteChunksInBatchesAsync(
        Guid uploadId, CancellationToken ct)
    {
        const string sql = """
            DELETE FROM "UploadChunks"
            WHERE "Id" IN (
                SELECT "Id" FROM "UploadChunks"
                WHERE "UploadId" = @uploadId
                LIMIT @batch
            )
            """;

        while (true)
        {
            var deleted = await _db.Database.ExecuteSqlRawAsync(
                sql,
                new object[]
                {
                    new NpgsqlParameter("uploadId", uploadId),
                    new NpgsqlParameter("batch", ChunkDeleteBatchSize),
                },
                ct);

            if (deleted == 0) break;
        }
    }

    private async Task<UploadSessionEntity> GetActiveSessionAsync(
        Guid uploadId, CancellationToken ct)
    {
        var session = await _db.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == uploadId, ct)
            ?? throw new UploadNotFoundException($"上传会话不存在：{uploadId}");

        if (session.Tenant != _caller.Tenant)
            throw new UploadNotFoundException($"上传会话不存在：{uploadId}");

        if (session.Status is "completed" or "failed" or "merging")
            throw new UploadConflictException(
                $"上传会话状态为 '{session.Status}'，不允许继续操作。");

        if (session.Status == "expired")
            throw new UploadGoneException("上传会话已过期。");

        if (session.ExpiresAt < DateTimeOffset.UtcNow)
        {
            await _db.UploadSessions
                .Where(s => s.Id == uploadId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "expired"), ct);
            throw new UploadGoneException("上传会话已过期。");
        }

        return session;
    }

    private async Task MarkFailedAsync(Guid uploadId, string reason)
    {
        try
        {
            // ★ 独立 Scope：请求 CT 取消后当前 DbContext 可能不可用
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();

            await db.UploadSessions
                .Where(s => s.Id == uploadId &&
                            (s.Status == "merging" || s.Status == "uploading"))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "failed")
                    .SetProperty(x => x.ErrorMessage,
                        reason.Length > 2000 ? reason[..2000] : reason)
                    .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow),
                    CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "标记上传失败时出错：{UploadId}", uploadId);
        }
    }

    private async Task<int> NextVersionAsync(
        string tenant, string docId, CancellationToken ct)
    {
        var maxVersion = await _db.DocumentMetadata
            .Where(d => d.Tenant == tenant && d.DocId == docId)
            .MaxAsync(d => (int?)d.Version, ct);

        return (maxVersion ?? 0) + 1;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";

    private static string SanitizeSegment(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "unknown";
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        var s = sb.ToString().Trim('.');
        return string.IsNullOrEmpty(s) ? "unknown" : s;
    }

    private static string BuildObjectKey(
        string tenant, string docId, int version, string fileName)
    {
        var safeTenant = SanitizeSegment(tenant);
        var safeDocId  = SanitizeSegment(docId);
        var safeName   = SanitizeSegment(fileName);
        return $"{safeTenant}/{safeDocId}/v{version}/{safeName}";
    }

    private static int NormalizeChunkSize(int? requested, long totalSize)
    {
        var chunkSize = Math.Clamp(requested ?? DefaultChunkSize, MinChunkSize, MaxChunkSize);

        const int maxChunks = 10000;
        if (totalSize / chunkSize > maxChunks)
            chunkSize = (int)Math.Ceiling((double)totalSize / maxChunks);

        return Math.Max(chunkSize, MinChunkSize);
    }
}