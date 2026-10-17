using Microsoft.Extensions.Options;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// 向量对账服务（最后防线）：周期扫描两侧差集并修复。
/// 场景：RabbitMQ 重启 / 消息丢失 / Qdrant 卷损坏。
///   - PG 有、Qdrant 无 → 补算向量
///   - Qdrant 有、PG 无 → 删除孤儿向量
/// </summary>
public sealed class VectorReconciliationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VectorSyncOptions _options;
    private readonly ILogger<VectorReconciliationService> _logger;

    public VectorReconciliationService(
        IServiceScopeFactory scopeFactory,
        IOptions<VectorSyncOptions> options,
        ILogger<VectorReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.ReconciliationIntervalHours <= 0)
        {
            _logger.LogInformation("向量对账已禁用（IntervalHours=0）");
            return;
        }

        // 首次延迟 10 分钟：等系统稳定
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "向量对账失败（下次周期重试）");
            }

            await Task.Delay(
                TimeSpan.FromHours(_options.ReconciliationIntervalHours),
                stoppingToken);
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var synchronizer = scope.ServiceProvider
            .GetRequiredService<VectorSynchronizer>();
        await synchronizer.ReconcileAllAsync(ct);
    }
}
