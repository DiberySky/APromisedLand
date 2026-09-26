using FileStorageApi.Data;         // FileStorageContext
using FileStorageApi.Entities;     // DocumentAuditEntity
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileStorageApi.Files;

public sealed class AuditWriterService : BackgroundService
{
    private const int MaxBatchSize = 64;

    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(2);

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

    // ══════════════════════════════════════════════════════════
    // 主循环：任何异常都不 rethrow —— 避免触发
    // Debugger.BreakForUserUnhandledException 在调试器下打断 F5。
    //
    // ★ 关于 batch.Count == 0 的语义（重要）：
    //   AuditQueue.DequeueBatchAsync 内部用 Channel.WaitToReadAsync：
    //     - channel 有数据       → 返回非空列表
    //     - channel 已 Complete  → 返回空列表
    //     - 没数据也没关闭       → 异步等待，不返回
    //   因此 "空列表" 的唯一含义是 channel 已关闭。
    //   此时必须 break，否则主循环会 100% CPU 忙等。
    // ══════════════════════════════════════════════════════════
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AuditWriter 已启动（批次 {BatchSize}，排空超时 {Timeout}）",
            MaxBatchSize, DrainTimeout);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await _queue.DequeueBatchAsync(MaxBatchSize, stoppingToken);

                // ★ 关键改动：空批次 → channel 已关闭 → 退出循环
                if (batch.Count == 0)
                    break;

                await WriteBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;   // 正常关闭
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuditWriter 周期失败，{Backoff} 后重试", ErrorBackoff);
                try
                {
                    await Task.Delay(ErrorBackoff, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    // 关闭时把队列里剩余事件写出去（最多 DrainTimeout）。
    // ══════════════════════════════════════════════════════════
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AuditWriter 关闭中，尝试排空剩余审计事件…");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DrainTimeout);

        try
        {
            await FlushAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AuditWriter 排空超时（{Timeout}），丢弃剩余事件", DrainTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuditWriter 排空时发生未预期错误");
        }

        await base.StopAsync(cancellationToken);
    }

    // ── 一次性排空 ──
    private async Task FlushAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var batch = await _queue.DequeueBatchAsync(MaxBatchSize, ct);
            if (batch.Count == 0) break;   // channel 关闭 / 已空
            await WriteBatchAsync(batch, ct);
        }
    }

    // ══════════════════════════════════════════════════════════
    // 真正落库：把队列条目转换为 EF 实体，AddRange + SaveChanges。
    // 每个批次开一个新的 scope —— FileStorageContext 是 scoped，
    // 而 AuditWriterService 是 singleton（IHostedService）。
    // ══════════════════════════════════════════════════════════
    private async Task WriteBatchAsync(
        IReadOnlyList<AuditEntry> batch,
        CancellationToken ct)
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
}