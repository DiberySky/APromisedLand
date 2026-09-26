using Microsoft.Extensions.Options;

namespace FileStorageApi.Uploads;

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

    // ══════════════════════════════════════════════════════════
    // 主循环：所有异常都不允许逃逸到宿主。
    //
    // ★ 关于 [DebuggerDisableUserUnhandledExceptions]：
    //   已彻底删除。该特性不是"让调试器忽略异常"，而是
    //   "当异常从本方法逃逸时，主动调用
    //   Debugger.BreakForUserUnhandledException"。
    //   保留它反而会让调试器在后台任务出问题时中断。
    //
    // ★ 关于 catch (Exception)：
    //   必须放在 ExecuteAsync 的最外层。BackgroundService 的
    //   ExecuteAsync 抛出的异常会被 Host 记录，但 .NET 9 的
    //   Debugger.BreakForUserUnhandledException 会在异常逃逸的
    //   瞬间触发，因此任何异常都不能离开此方法。
    // ══════════════════════════════════════════════════════════
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;

        if (!opts.Enabled)
        {
            _logger.LogInformation("上传会话清理服务已禁用。");
            return;
        }

        var configuredDelay = opts.EffectiveStartupDelay;
        var effectiveDelay  = configuredDelay < TimeSpan.FromMinutes(2)
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 正常关闭，不记录为错误
        }
        catch (Exception ex)
        {
            // 兜底：不让任何异常逃逸到宿主，
            // 避免触发 BreakForUserUnhandledException。
            _logger.LogError(ex, "上传会话清理服务遇到未预期异常，即将退出。");
        }
        finally
        {
            _logger.LogInformation("上传会话清理服务已停止。");
        }
    }

    // ══════════════════════════════════════════════════════════
    // 单次清理。返回 false 表示应终止主循环。
    // ══════════════════════════════════════════════════════════
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
            return false;   // 停机期间取消 → 正常结束
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "清理过期上传会话失败（已运行 {ElapsedMs} ms），将在下一轮重试。",
                sw.ElapsedMilliseconds);
            return true;    // 失败不终止循环
        }
    }
}