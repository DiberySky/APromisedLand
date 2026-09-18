using Microsoft.Extensions.Options;

namespace FileStorageApi.Uploads;

/// <summary>
/// 上传会话清理后台服务。
///
/// 职责（委托给 IFileUploadService.CleanupExpiredAsync）：
///   1. 过期会话：ExpiresAt &lt; now 且非 completed / merging
///      → 删除 UploadChunks + UploadSessions
///   2. completed 会话保留期：CompletedAt &lt; now - CompletedRetention
///      → 删除 UploadSessions 行（分块已在完成时清空）
///   3. stale merging：Status = merging 且 UpdatedAt 超过阈值（默认 1 小时）
///      → 置 failed，避免并发冲突后会话卡死
///   4. stale pending_upload：DocumentMetadata.Status = pending_upload
///      且 UpdatedAt 超过阈值（默认 2 小时）
///      → 删除 S3 对象 + 删除元数据行，回收孤儿对象
///      （S3 删除失败时保留元数据延后重试）
///   5. delete_pending 恢复：重试 S3 删除，成功后置 deleted
///   6. stale failed 分块清理：删除分块，会话置 cleaned
///
/// 运行策略：
///   - 启动后延迟 StartupDelay（默认 5 分钟），避免与启动预热/迁移竞争
///   - 每隔 Interval（默认 1 小时）执行一次
///   - 单次执行失败不退出循环，记录日志后等待下一轮
///   - 关闭时立即响应 CancellationToken，不阻塞应用优雅停止
///
/// 并发安全：
///   - 使用 IServiceScopeFactory 创建独立 Scope，
///     避免与请求级 DbContext 冲突
///   - 清理逻辑本身通过 ExecuteUpdate/ExecuteDelete 与
///     单事务完成，多实例部署时同一行只会被删一次
///
/// 取消语义（重要）：
///   - 本服务在任何路径下都不向 BackgroundService 抛出异常。
///   - RunOnceAsync 内部对"关闭取消"仅返回 false，不 rethrow，
///     避免 VS 调试器在 System.Diagnostics.Debugger.BreakForUserUnhandledException
///     断下（该断点判定基于方法内是否出现 throw，而非最终是否被上层接住）。
///
/// 配置项（appsettings.json → "UploadCleanup"）：
///   - Enabled                   是否启用（默认 true）
///   - IntervalMinutes           执行间隔分钟数（默认 60，最小 1）
///   - StartupDelayMinutes       启动延迟分钟数（默认 5，最小 0）
///   - StaleMergingMinutes       merging 超时阈值（默认 60）
///   - StalePendingMinutes       pending_upload 超时阈值（默认 120）
///   - CompletedRetentionMinutes completed 保留期（默认 10080，0 禁用）
///   - BatchSize                 单批处理行数（默认 500）
///   - OrphanDeleteConcurrency   S3 删除并发度（默认 4）
/// </summary>
public sealed class UploadSessionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<UploadCleanupOptions> _options;
    private readonly ILogger<UploadSessionCleanupService> _logger;

    public UploadSessionCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<UploadCleanupOptions> options,
        ILogger<UploadSessionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options      = options;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;

        if (!opts.Enabled)
        {
            _logger.LogInformation("上传会话清理服务已禁用。");
            return;
        }

        // ★ 保底：不管配置怎么读，至少延迟 2 分钟启动
        //    避开启动期健康检查、EF 迁移的连接争抢
        var configuredDelay = opts.EffectiveStartupDelay;
        var effectiveDelay = configuredDelay < TimeSpan.FromMinutes(2)
            ? TimeSpan.FromMinutes(2)
            : configuredDelay;

        var interval = opts.EffectiveInterval;

        _logger.LogInformation(
            "上传会话清理服务已启动：启动延迟 {StartupDelay}（配置值 {Configured}），执行间隔 {Interval}。",
            effectiveDelay, configuredDelay, interval);

        try
        {
            if (effectiveDelay > TimeSpan.Zero)
                await Task.Delay(effectiveDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await RunOnceAsync(stoppingToken))
                    break;

                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            _logger.LogInformation("上传会话清理服务已停止。");
        }
    }

    /// <summary>
    /// 执行一次清理。**本方法不抛出任何异常**。
    ///
    /// 返回值：
    ///   true  —— 本轮已结束（无论成功或失败），主循环应继续下一轮；
    ///   false —— 关闭取消，主循环应立即退出。
    /// </summary>
    private async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uploads = scope.ServiceProvider
                .GetRequiredService<IFileUploadService>();

            _logger.LogDebug("开始清理过期上传会话…");

            var removed = await uploads.CleanupExpiredAsync(stoppingToken);

            sw.Stop();

            if (removed > 0)
            {
                _logger.LogInformation(
                    "清理完成：删除 {Removed} 个过期会话，耗时 {ElapsedMs} ms。",
                    removed, sw.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogDebug(
                    "清理完成：无可清理会话，耗时 {ElapsedMs} ms。",
                    sw.ElapsedMilliseconds);
            }

            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 关闭中：不 rethrow、不记 Error，返回 false 让主循环退出。
            return false;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "清理过期上传会话失败（已运行 {ElapsedMs} ms），将在下一轮重试。",
                sw.ElapsedMilliseconds);
            return true; // 继续下一轮
        }
    }
}