using FileStorageApi.Data;
using FileStorageApi.Storage;
using Microsoft.Extensions.Options;

namespace FileStorageApi.Files;

/// <summary>
/// 补偿清理：重试 status=delete_pending 超过阈值的文档对象，
/// 再次尝试真正删除 S3 对象并更新 status。
///
/// 触发场景：级联删除（EAV SaveChanges 后调 DELETE /Files/by-doc）时
/// S3 删除瞬时失败（网络抖动/MinIO 重启），status 已置 delete_pending
/// 但对象还在 S3。
///
/// 模式：与 UploadSessionCleanupService 完全一致——
///   IServiceScopeFactory + BackgroundService + Options + while(!stopping) + 异常不外逃。
/// </summary>
public sealed class DeletePendingRetryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DeleteRetryOptions _options;
    private readonly ILogger<DeletePendingRetryService> _logger;

    public DeletePendingRetryService(
        IServiceScopeFactory scopeFactory,
        IOptions<DeleteRetryOptions> options,
        ILogger<DeletePendingRetryService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("DeletePending 重试服务已禁用。");
            return;
        }

        var startupDelay = _options.StartupDelay < TimeSpan.FromMinutes(2)
            ? TimeSpan.FromMinutes(2)
            : _options.StartupDelay;

        _logger.LogInformation(
            "DeletePending 重试服务已启动：启动延迟 {StartupDelay}，执行间隔 {Interval}。",
            startupDelay, _options.Interval);

        try
        {
            if (startupDelay > TimeSpan.Zero)
                await Task.Delay(startupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await RunOnceAsync(stoppingToken)) break;
                await Task.Delay(_options.Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeletePending 重试服务遇到未预期异常，即将退出。");
        }
        finally
        {
            _logger.LogInformation("DeletePending 重试服务已停止。");
        }
    }

    private async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IFileMetadataService>();
            var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();

            var cutoff = DateTimeOffset.UtcNow - _options.PendingThreshold;
            var pending = await svc.ListDeletePendingAsync(cutoff, stoppingToken);

            if (pending.Count == 0)
            {
                _logger.LogDebug("DeletePending：无可重试对象。");
                return true;
            }

            var success = 0;
            var stillFail = 0;

            foreach (var m in pending)
            {
                try
                {
                    var ok = await storage.DeleteAsync(m.ObjectKey, stoppingToken);
                    m.Status = ok ? "deleted" : "delete_pending";
                    m.UpdatedAt = DateTimeOffset.UtcNow;

                    if (ok) success++; else stillFail++;
                }
                catch (Exception ex)
                {
                    stillFail++;
                    _logger.LogWarning(ex,
                        "DeletePending 重试跳过：docId={DocId}, objectKey={Key}",
                        m.DocId, m.ObjectKey);

                    // 仍留在 delete_pending，下次再试
                    m.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            await db.SaveChangesAsync(stoppingToken);

            sw.Stop();
            _logger.LogInformation(
                "DeletePending 重试完成：共 {Total}，成功 {Success}，仍失败 {Fail}，耗时 {ElapsedMs} ms。",
                pending.Count, success, stillFail, sw.ElapsedMilliseconds);

            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "DeletePending 重试失败（已运行 {ElapsedMs} ms），将在下一轮重试。",
                sw.ElapsedMilliseconds);
            return true;
        }
    }
}

public class DeleteRetryOptions
{
    public const string SectionName = "DeleteRetry";

    /// <summary>默认启用。设为 false 禁用整个服务。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>应用启动后多久开始首次清理（默认 3 分钟）。</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>清理执行间隔（默认 5 分钟）。</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>delete_pending 状态超过多久才进入重试队列（默认 5 分钟）。</summary>
    public TimeSpan PendingThreshold { get; set; } = TimeSpan.FromMinutes(5);
}
