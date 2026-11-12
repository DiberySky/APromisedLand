using Microsoft.Extensions.Options;

namespace TreeGraph.Api.GraphSyncSky.SpaceNode;

/// <summary>
/// SpaceNode 派生 Tag 周期对账器（S4.5）：无发件箱下的自愈兜底。
/// 固定间隔执行一轮 <see cref="SpaceNodeTagReconcileService.ReconcileAsync"/>（幂等）。
/// 首轮延迟一个间隔再执行（启动对齐已由 GraphSchemaInitializer 负责）。
/// </summary>
public sealed class SpaceNodeTagReconciler : BackgroundService
{
    /// <summary>对账间隔（配置 GraphSync:ReconcileIntervalSeconds，默认 300 秒）。</summary>
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GraphSyncOptions _options;
    private readonly ILogger<SpaceNodeTagReconciler> _logger;

    public SpaceNodeTagReconciler(
        IServiceScopeFactory scopeFactory,
        IOptions<GraphSyncOptions> options,
        ILogger<SpaceNodeTagReconciler> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            _options.ReconcileIntervalSeconds > 0 ? _options.ReconcileIntervalSeconds : 300);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<SpaceNodeTagReconcileService>();
                var result = await service.ReconcileAsync(stoppingToken);
                if (result.Errors.Count > 0)
                    _logger.LogWarning(
                        "SpaceNode Tag 周期对账含 {Count} 个错误：{First}",
                        result.Errors.Count, result.Errors[0]);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 对账失败不中断后台循环，下一轮自愈
                _logger.LogError(ex, "SpaceNode Tag 周期对账失败，等待下一轮");
            }
        }
    }
}
