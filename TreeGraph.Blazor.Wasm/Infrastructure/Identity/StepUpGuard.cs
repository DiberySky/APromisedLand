namespace TreeGraph.Blazor.Infrastructure.Identity;

/// <summary>
/// WASM 不在浏览器判断认证新鲜度。管理 API 对高危路由强制执行 Step-Up，
/// HTTP 客户端收到 428 后统一发起 max_age=0 的重新认证。
/// </summary>
public sealed class StepUpGuard
{
    public Task<bool> EnsureFreshAsync(
        string action,
        string returnUrl,
        string? payload = null,
        CancellationToken ct = default)
        => Task.FromResult(true);
}
