using APromisedLand.Api.Data;
using APromisedLand.Api.MafRag.Entities;
using Microsoft.EntityFrameworkCore;

namespace MAFRagService.Services;

/// <summary>
/// 索引任务状态追踪。所有状态变更都写 IndexTaskEntity，
/// 不污染 DocumentMetadataEntity.Status（那是文档生命周期字段）。
/// </summary>
public sealed class IndexTaskService
{
    public const string StatusPending   = "pending";
    public const string StatusRunning   = "running";
    public const string StatusCompleted = "completed";
    public const string StatusFailed    = "failed";
    public const string StatusCancelled = "cancelled";

    private readonly MafRagContext _db;

    public IndexTaskService(MafRagContext db) => _db = db;

    /// <summary>
    /// 幂等创建：同一 (DocId, Tenant, TaskType) 若已有 Running/Pending 任务，
    /// 复用该记录并累加 RetryCount，避免重复入队产生重复行。
    /// </summary>
    public async Task<IndexTaskEntity> BeginAsync(
        string docId, string tenant, string taskType, CancellationToken ct)
    {
        var existing = await _db.IndexTasks
            .Where(t => t.DocId == docId
                     && t.Tenant == tenant
                     && t.TaskType == taskType
                     && (t.Status == StatusPending || t.Status == StatusRunning))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            existing.Status      = StatusRunning;
            existing.RetryCount += 1;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var entity = new IndexTaskEntity
        {
            DocId    = docId,
            Tenant   = tenant,
            TaskType = taskType,
            Status   = StatusRunning
        };
        _db.IndexTasks.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task CompleteAsync(Guid taskId, CancellationToken ct)
    {
        var entity = await _db.IndexTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (entity is null) return;

        entity.Status       = StatusCompleted;
        entity.CompletedAt  = DateTime.UtcNow;
        entity.ErrorMessage = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task FailAsync(Guid taskId, string error, CancellationToken ct)
    {
        var entity = await _db.IndexTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (entity is null) return;

        entity.Status       = StatusFailed;
        entity.ErrorMessage = error.Length > 2000 ? error[..2000] : error;
        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid taskId, CancellationToken ct)
    {
        var entity = await _db.IndexTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (entity is null) return;

        entity.Status      = StatusCancelled;
        entity.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>供 /rag/docs/{docId}/status 查询。</summary>
    public async Task<IReadOnlyList<IndexTaskEntity>> ListByDocAsync(
        string docId, string tenant, CancellationToken ct)
        => await _db.IndexTasks
            .Where(t => t.DocId == docId && t.Tenant == tenant)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
}