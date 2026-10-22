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
    /// 幂等创建：space（FIXED_STRING(64) vid，覆盖 GUID）、6 个节点 tag、
    /// parent_of 树父子边、line_contains / connects_to 线路结构边、
    /// PG edge_definitions 中全部语义边类型。
    /// 已有 space 的 line_node 缺新列时用 DESCRIBE + ALTER 补齐。
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
            var cols = GraphNaming.TagColumns[tag]
                .Select(name => $"`{name}` {(name == "is_archived" ? "bool" : "string")} NULL")
                .ToList();
            await _client.QueryAsync(
                $"CREATE TAG IF NOT EXISTS `{tag}`({string.Join(", ", cols)})", ct);
        }

        // 存量图空间：line_node / line 新增列不会因 IF NOT EXISTS 自动补，DESCRIBE 后 ALTER。
        foreach (var tag in new[] { GraphNaming.LineNodeTag, GraphNaming.LineTag })
            await EnsureTagColumnsAsync(tag, ct);

        await _client.QueryAsync($"CREATE EDGE IF NOT EXISTS `{GraphNaming.ParentOfEdge}`()", ct);
        await _client.QueryAsync($"CREATE EDGE IF NOT EXISTS `{GraphNaming.LineContainsEdge}`()", ct);
        await _client.QueryAsync(
            $"CREATE EDGE IF NOT EXISTS `{GraphNaming.ConnectsToEdge}`(`role` string NULL, `operating_state` string NULL)", ct);

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

    /// <summary>DESCRIBE TAG 后补齐缺失列（新增列类型统一 string NULL）。</summary>
    private async Task EnsureTagColumnsAsync(string tag, CancellationToken ct)
    {
        var described = await _client.QueryAsync($"DESCRIBE TAG `{tag}`", ct);
        var existing = described.Data?
            .Select(row => row.TryGetValue("Field", out var f) ? f.GetString() : null)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var missing = GraphNaming.TagColumns[tag]
            .Where(col => !existing.Contains(col))
            .Select(col => $"`{col}` string NULL")
            .ToList();
        if (missing.Count == 0) return;

        await _client.QueryAsync(
            $"ALTER TAG `{tag}` ADD ({string.Join(", ", missing)})", ct);
        _logger.LogInformation("NebulaGraph tag {Tag} 补齐列：{Columns}", tag, string.Join(",", missing));
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
