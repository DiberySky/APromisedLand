using FileStorageApi.Data;
using FileStorageApi.Entities;

namespace FileStorageApi.Files;

/// <summary>
/// 审计写入后台服务。批量消费 <see cref="AuditQueue"/>，
/// 每批最多 64 条，一次 SaveChanges。
///
/// 与 FileMetadataService.DownloadAsync 中的无界 Task.Run 相比：
///   - 并发度有界（单读者）
///   - DB 压力可控（批量提交）
///   - 应用关闭时可等待队列排空（见 ShutdownTimeout）
/// </summary>
public sealed class AuditWriterService : BackgroundService
{
    private const int MaxBatchSize = 64;
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private readonly AuditQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditWriterService> _logger;

    public AuditWriterService(
        AuditQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditWriterService> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var buffer = new List<AuditEntry>(MaxBatchSize);

        try
        {
            await foreach (var entry in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                buffer.Add(entry);

                // 尽量同步多读，凑满一批
                while (buffer.Count < MaxBatchSize &&
                       _queue.Reader.TryRead(out var next))
                {
                    buffer.Add(next);
                }

                await FlushAsync(buffer, stoppingToken);
                buffer.Clear();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 关闭中：尝试排空剩余条目
            try
            {
                using var drainCts = new CancellationTokenSource(DrainTimeout);

                while (_queue.Reader.TryRead(out var remaining))
                {
                    buffer.Add(remaining);
                    if (buffer.Count >= MaxBatchSize)
                    {
                        await FlushAsync(buffer, drainCts.Token);
                        buffer.Clear();
                    }
                }

                if (buffer.Count > 0)
                    await FlushAsync(buffer, drainCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "审计队列排空失败，剩余 {Count} 条丢弃。", buffer.Count);
            }
        }
    }

    private async Task FlushAsync(List<AuditEntry> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();

            foreach (var e in batch)
            {
                db.DocumentAudits.Add(new DocumentAuditEntity
                {
                    DocId       = e.DocId,
                    Tenant      = e.Tenant,
                    Action      = e.Action,
                    Actor       = e.Actor,
                    DetailsJson = e.DetailsJson,
                });
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "批量写审计失败，丢弃 {Count} 条。", batch.Count);
        }
    }
}