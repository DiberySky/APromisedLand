using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.GraphSyncSky;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// 生产网关：经 TreeGraph.NebulaFastApi REST 执行 CREATE/ALTER/DROP EDGE。
/// DDL 异步生效（官方建议等约两个心跳周期），执行后轮询 DESCRIBE 确认生效，
/// 与 GraphSchemaInitializer 的轮询惯例一致；属性名/类型名由服务层按
/// Nebula 命名规则（字母开头，仅字母/数字/下划线）校验，无需额外转义。
/// </summary>
public sealed class NebulaEdgeSchemaGateway : IEdgeSchemaGateway
{
    private readonly NebulaGraphClient _client;
    private readonly ILogger<NebulaEdgeSchemaGateway> _logger;

    public NebulaEdgeSchemaGateway(NebulaGraphClient client, ILogger<NebulaEdgeSchemaGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task CreateEdgeTypeAsync(EdgeType type, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(EdgeNgqlDdlBuilder.BuildCreateEdgeNgql(type), ct);
        await WaitUntilAsync(
            async () => await DescribeColumnsAsync(type.Name, ct) is not null,
            $"CREATE EDGE {type.Name}", ct);
    }

    public async Task AlterAddPropsAsync(string edgeType, IReadOnlyList<EdgeTypeProp> props, CancellationToken ct)
    {
        if (props.Count == 0) return;
        var cols = string.Join(", ", props.Select(EdgeNgqlDdlBuilder.BuildColumnDef));
        await ExecuteWithRetryAsync($"ALTER EDGE `{edgeType}` ADD ({cols})", ct);
        await WaitUntilAsync(async () =>
        {
            var existing = await DescribeColumnsAsync(edgeType, ct);
            return existing is not null && props.All(p => existing.Contains(p.PropName));
        }, $"ALTER EDGE {edgeType} ADD", ct);
    }

    public async Task AlterDropPropsAsync(string edgeType, IReadOnlyList<string> propNames, CancellationToken ct)
    {
        if (propNames.Count == 0) return;
        var cols = string.Join(", ", propNames.Select(n => $"`{n}`"));
        await ExecuteWithRetryAsync($"ALTER EDGE `{edgeType}` DROP ({cols})", ct);
        await WaitUntilAsync(async () =>
        {
            var existing = await DescribeColumnsAsync(edgeType, ct);
            return existing is not null && !propNames.Any(existing.Contains);
        }, $"ALTER EDGE {edgeType} DROP", ct);
    }

    public async Task AlterChangePropTypeAsync(string edgeType, string propName, string newDataType, CancellationToken ct)
    {
        await ExecuteWithRetryAsync($"ALTER EDGE `{edgeType}` CHANGE (`{propName}` {newDataType})", ct);
        await WaitUntilAsync(async () =>
        {
            var types = await DescribeColumnTypesAsync(edgeType, ct);
            return types is not null
                   && types.TryGetValue(propName, out var actual)
                   && actual.Contains(newDataType, StringComparison.OrdinalIgnoreCase);
        }, $"ALTER EDGE {edgeType} CHANGE {propName}", ct);
    }

    public async Task DropEdgeTypeAsync(string edgeType, CancellationToken ct)
    {
        await ExecuteWithRetryAsync($"DROP EDGE IF EXISTS `{edgeType}`", ct);
        await WaitUntilAsync(
            async () => await DescribeColumnsAsync(edgeType, ct) is null,
            $"DROP EDGE {edgeType}", ct);
    }

    // ------------------------------------------------------------
    // 重试与生效确认（DDL 生成见 EdgeNgqlDdlBuilder）
    // ------------------------------------------------------------

    /// <summary>DDL 幂等重试（IF NOT EXISTS / IF EXISTS 语义；space 刚重建时 metad 可能瞬时失败）。</summary>
    private async Task ExecuteWithRetryAsync(string ngql, CancellationToken ct)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _client.QueryAsync(ngql, ct);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested)
            {
                _logger.LogWarning("nGQL 第 {Attempt}/{Max} 次失败：{Msg} | {Ngql}", attempt, maxAttempts, ex.Message, ngql);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    /// <summary>轮询确认 DDL 已生效（2s × 15 ≈ 覆盖 30s 传播窗口），超时抛 InvalidOperationException。</summary>
    private async Task WaitUntilAsync(Func<Task<bool>> verify, string what, CancellationToken ct)
    {
        const int maxAttempts = 15;
        for (var attempt = 1; ; attempt++)
        {
            if (await verify())
            {
                if (attempt > 1)
                    _logger.LogInformation("NebulaGraph DDL 生效确认：{What}（第 {Attempt} 次）", what, attempt);
                return;
            }
            if (attempt >= maxAttempts)
                throw new InvalidOperationException($"NebulaGraph DDL 生效超时：{what}");
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }

    /// <summary>DESCRIBE EDGE 取列名集；边类型不存在（含传播窗口内）返回 null。</summary>
    private async Task<HashSet<string>?> DescribeColumnsAsync(string edgeType, CancellationToken ct)
    {
        var types = await DescribeColumnTypesAsync(edgeType, ct);
        return types?.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>DESCRIBE EDGE 取列名 → 类型映射；边类型不存在（含传播窗口内）返回 null。</summary>
    private async Task<Dictionary<string, string>?> DescribeColumnTypesAsync(string edgeType, CancellationToken ct)
    {
        try
        {
            var result = await _client.QueryAsync($"DESCRIBE EDGE `{edgeType}`", ct);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in result.Data ?? [])
            {
                if (row.TryGetValue("Field", out var f) && row.TryGetValue("Type", out var t))
                {
                    var name = f.GetString();
                    var type = t.GetString();
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(type))
                        map[name] = type;
                }
            }
            return map;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
