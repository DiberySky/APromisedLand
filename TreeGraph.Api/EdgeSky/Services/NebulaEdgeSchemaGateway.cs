using System.IO;
using System.Net.Http;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.GraphSyncSky;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// 生产网关：经 TreeGraph.NebulaFastApi REST 执行 CREATE/ALTER/DROP EDGE。
/// EdgeType 从属于图空间：spaceId 经 <see cref="GraphNaming.ToNebulaSpace"/> 路由到
/// 对应 Nebula space。DDL 异步生效（官方建议等约两个心跳周期），执行后轮询 DESCRIBE
/// 确认生效，与 GraphSchemaInitializer 的轮询惯例一致；属性名/类型名由服务层按
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
        var space = GraphNaming.ToNebulaSpace(type.SpaceId);
        await ExecuteWithRetryAsync(space, EdgeNgqlDdlBuilder.BuildCreateEdgeNgql(type), ct);
        await WaitUntilAsync(
            async () => await DescribeColumnsAsync(space, type.Name, ct) is not null,
            $"CREATE EDGE {type.Name}", ct);
    }

    public async Task AlterAddPropsAsync(string spaceId, string edgeType, IReadOnlyList<EdgeTypeProp> props, CancellationToken ct)
    {
        if (props.Count == 0) return;
        var space = GraphNaming.ToNebulaSpace(spaceId);
        var cols = string.Join(", ", props.Select(EdgeNgqlDdlBuilder.BuildColumnDef));
        await ExecuteWithRetryAsync(space, $"ALTER EDGE `{edgeType}` ADD ({cols})", ct);
        await WaitUntilAsync(async () =>
        {
            var existing = await DescribeColumnsAsync(space, edgeType, ct);
            return existing is not null && props.All(p => existing.Contains(p.PropName));
        }, $"ALTER EDGE {edgeType} ADD", ct);
    }

    public async Task AlterDropPropsAsync(string spaceId, string edgeType, IReadOnlyList<string> propNames, CancellationToken ct)
    {
        if (propNames.Count == 0) return;
        var space = GraphNaming.ToNebulaSpace(spaceId);
        var cols = string.Join(", ", propNames.Select(n => $"`{n}`"));
        await ExecuteWithRetryAsync(space, $"ALTER EDGE `{edgeType}` DROP ({cols})", ct);
        await WaitUntilAsync(async () =>
        {
            var existing = await DescribeColumnsAsync(space, edgeType, ct);
            return existing is not null && !propNames.Any(existing.Contains);
        }, $"ALTER EDGE {edgeType} DROP", ct);
    }

    public async Task DropEdgeTypeAsync(string spaceId, string edgeType, CancellationToken ct)
    {
        var space = GraphNaming.ToNebulaSpace(spaceId);
        await ExecuteWithRetryAsync(space, $"DROP EDGE IF EXISTS `{edgeType}`", ct);
        await WaitUntilAsync(
            async () => await DescribeColumnsAsync(space, edgeType, ct) is null,
            $"DROP EDGE {edgeType}", ct);
    }

    // ------------------------------------------------------------
    // 重试与生效确认（DDL 生成见 EdgeNgqlDdlBuilder）
    // ------------------------------------------------------------

    /// <summary>
    /// DDL 幂等重试（IF NOT EXISTS / IF EXISTS 语义；space 刚重建时 metad 可能瞬时失败）。
    /// 仅对瞬态异常（网络/IO/超时）重试；语义/校验异常立即抛出，避免无效重试。
    /// </summary>
    private async Task ExecuteWithRetryAsync(string space, string ngql, CancellationToken ct)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _client.QueryAsync(space, ngql, ct);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested && IsTransient(ex))
            {
                _logger.LogWarning("nGQL 第 {Attempt}/{Max} 次失败：{Msg} | {Ngql}", attempt, maxAttempts, ex.Message, ngql);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    /// <summary>瞬态异常判定：网络/IO 故障与非用户取消的超时；语义异常（InvalidOperationException/ArgumentException）不重试。</summary>
    private static bool IsTransient(Exception ex) => ex is HttpRequestException or IOException
        || (ex is TaskCanceledException tce && !tce.CancellationToken.IsCancellationRequested);

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
    private async Task<HashSet<string>?> DescribeColumnsAsync(string space, string edgeType, CancellationToken ct)
    {
        var types = await DescribeColumnTypesAsync(space, edgeType, ct);
        return types?.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>DESCRIBE EDGE 取列名 → 类型映射；边类型不存在（含传播窗口内）或查询异常均返回 null。</summary>
    private async Task<Dictionary<string, string>?> DescribeColumnTypesAsync(string space, string edgeType, CancellationToken ct)
    {
        try
        {
            var result = await _client.QueryAsync(space, $"DESCRIBE EDGE `{edgeType}`", ct);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 传播窗口内 DESCRIBE 可能抛多种异常（不存在/未就绪），统一视为"尚未可见"交由 WaitUntil 轮询
            _logger.LogDebug(ex, "DESCRIBE EDGE {EdgeType} 暂不可用（可能不存在或传播中）", edgeType);
            return null;
        }
    }
}
