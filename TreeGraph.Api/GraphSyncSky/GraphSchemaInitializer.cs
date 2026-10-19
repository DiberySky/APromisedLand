using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;

namespace TreeGraph.Api.GraphSyncSky;

/// <summary>
/// NebulaGraph schema 初始化器：启动时（后台重试）确保 space/5 个 tag/parent_of +
/// 全部语义边类型存在（幂等 IF NOT EXISTS）。
/// 消费者处理首条消息前调用 <see cref="EnsureReadyAsync"/> 等待就绪；
/// 全量重建后调用 <see cref="RecreateSchemaAsync"/> 重建 schema。
/// </summary>
public sealed class GraphSchemaInitializer : IHostedService, IDisposable
{
    private readonly NebulaGraphClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GraphSyncOptions _options;
    private readonly ILogger<GraphSchemaInitializer> _logger;
    private readonly CancellationTokenSource _stopCts = new();
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GraphSchemaInitializer(
        NebulaGraphClient client,
        IServiceScopeFactory scopeFactory,
        IOptions<GraphSyncOptions> options,
        ILogger<GraphSchemaInitializer> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        // graphd 启动 + ADD HOSTS + CREATE SPACE 生效均有延迟，后台重试不阻塞宿主启动
        _ = Task.Run(async () =>
        {
            try
            {
                await CreateSchemaWithRetryAsync(_stopCts.Token);
                _ready.TrySetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NebulaGraph schema 初始化失败，图同步暂不可用");
                _ready.TrySetException(ex);
            }
        }, CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _stopCts.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose() => _stopCts.Dispose();

    public Task EnsureReadyAsync(CancellationToken ct = default)
        => _ready.Task.WaitAsync(ct);

    /// <summary>重建 schema（全量重建 DROP SPACE 之后调用）。</summary>
    public Task RecreateSchemaAsync(CancellationToken ct)
        => CreateSchemaWithRetryAsync(ct);

    /// <summary>
    /// 幂等创建：space（FIXED_STRING(64) vid，覆盖 GUID）、5 个节点 tag、
    /// parent_of 树父子边、PG edge_definitions 中全部语义边类型。
    /// CREATE SPACE 生效需约两个心跳周期，失败由调用方重试。
    /// </summary>
    private async Task CreateSchemaOnceAsync(CancellationToken ct)
    {
        var space = _options.Space;
        await _client.QueryAsync(
            $"CREATE SPACE IF NOT EXISTS `{space}` " +
            "(partition_num=10, replica_factor=1, vid_type=FIXED_STRING(64))", ct);

        foreach (var tag in GraphNaming.AllNodeTags)
        {
            await _client.QueryAsync(
                $"CREATE TAG IF NOT EXISTS `{tag}`(" +
                "`name` string NULL, `node_type` string NULL, `is_archived` bool NULL)", ct);
        }

        await _client.QueryAsync($"CREATE EDGE IF NOT EXISTS `{GraphNaming.ParentOfEdge}`()", ct);

        // 语义边类型：以 PG edge_definitions 为准
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
        var typeNames = await db.EdgeDefinitions
            .AsNoTracking()
            .Select(d => d.TypeName)
            .ToListAsync(ct);

        foreach (var typeName in typeNames.Distinct())
        {
            await _client.QueryAsync(
                $"CREATE EDGE IF NOT EXISTS `{typeName}`(" +
                "`edge_id` string NULL, `weight` double NULL, `display_name` string NULL)", ct);
        }
    }

    private async Task CreateSchemaWithRetryAsync(CancellationToken ct)
    {
        const int maxAttempts = 24; // 约 2 分钟
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await CreateSchemaOnceAsync(ct);
                if (attempt > 1)
                    _logger.LogInformation("NebulaGraph schema 初始化第 {Attempt} 次成功", attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "NebulaGraph schema 初始化第 {Attempt}/{Max} 次失败：{Msg}，5s 后重试",
                    attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}
