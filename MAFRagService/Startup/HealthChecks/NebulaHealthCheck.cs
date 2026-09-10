using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MAFRagService.Startup.HealthChecks;

/// <summary>
/// NebulaGraph 健康检查（TCP 探测版）。
///
/// 判定逻辑：
///   · 从 ConnectionStrings:nebula 解析出 host / port。
///   · 通过 TCP 连接尝试，能在 3 秒内建立连接即视为可达。
///   · 不依赖 NebulaGraphExecutor 的具体 API，规避方言耦合。
///
/// 为什么用 TCP 而不是 HTTP：
///   · Nebula 的 HTTP 端点在探测语句下可能返回 4xx（取决于版本），
///     把 4xx 当失败会误报；TCP 层只判"服务在听"，语义更稳定。
///
/// 前置条件：
///   Program.cs 中连接串回退后必须回写 IConfiguration：
///       config["ConnectionStrings:nebula"] = conns.Nebula;
///   否则本检查会因读到 null 而抛 InvalidOperationException。
/// </summary>
public sealed class NebulaHealthCheck : IHealthCheck
{
    private const int DefaultPort = 9669;

    private readonly string   _host;
    private readonly int      _port;
    private readonly TimeSpan _timeout;

    public NebulaHealthCheck(IConfiguration config)
        : this(config, TimeSpan.FromSeconds(3))
    {
    }

    public NebulaHealthCheck(IConfiguration config, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(config);

        var raw = config.GetConnectionString("nebula");
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "缺少 ConnectionStrings:nebula。" +
                "请在 Program.cs 中回写解析后的值，或在 appsettings 显式配置。");
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:nebula 无法解析为 URI：{raw}");
        }

        _host    = uri.Host;
        _port    = uri.Port > 0 ? uri.Port : DefaultPort;
        _timeout = timeout;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["host"]       = _host,
            ["port"]       = _port,
            ["timeout_ms"] = _timeout.TotalMilliseconds
        };

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_timeout);

        var sw = Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(_host, _port, linkedCts.Token);

            sw.Stop();
            data["elapsed_ms"] = sw.ElapsedMilliseconds;

            return HealthCheckResult.Healthy(
                $"Nebula TCP {_host}:{_port} 可达", data);
        }
        catch (OperationCanceledException)
            when (linkedCts.IsCancellationRequested
                  && !cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            data["elapsed_ms"] = sw.ElapsedMilliseconds;

            return HealthCheckResult.Degraded(
                $"Nebula 探测超时（>{_timeout.TotalSeconds:F0}s）", data: data);
        }
        catch (Exception ex)
        {
            sw.Stop();
            data["elapsed_ms"] = sw.ElapsedMilliseconds;
            data["error"]      = ex.Message;

            return HealthCheckResult.Degraded(
                "Nebula 健康检查异常", ex, data);
        }
    }
}