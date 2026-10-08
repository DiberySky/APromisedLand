using TreeGraph.FileStorageApi.Security;

namespace TreeGraph.FileStorageApi.Tests.Fixtures;

/// <summary>
/// ICallerContext 的可变测试实现。
/// 注册为 scoped：每个 DI scope 拿到独立实例（Tenant 默认 "default"），
/// Tier 2 服务级测试在 scope 内直接改 Tenant / Actor 做租户隔离验证，
/// 不会泄漏到 HTTP 请求或其他测试。
/// </summary>
public sealed class TestCallerContext : ICallerContext
{
    public string Tenant { get; set; } = "default";
    public string? Actor { get; set; }
    public bool IsAuthenticated { get; set; }
}
