using Microsoft.Extensions.Options;
using System.Diagnostics;                    // ★ 新增

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

    [DebuggerDisableUserUnhandledExceptions]                    // ★
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;

        if (!opts.Enabled)
        {
            _logger.LogInformation("上传会话清理服务已禁用。");
            return;
        }

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

    [DebuggerDisableUserUnhandledExceptions]                    // ★
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
            return false;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "清理过期上传会话失败（已运行 {ElapsedMs} ms），将在下一轮重试。",
                sw.ElapsedMilliseconds);
            return true;
        }
    }
}