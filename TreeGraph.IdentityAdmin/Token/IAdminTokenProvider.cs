namespace TreeGraph.IdentityAdmin.Token;

/// <summary>
/// 管理 API 访问令牌提供器。令牌仅存在于服务端内存，不落盘、不进日志。
/// </summary>
public interface IAdminTokenProvider
{
    /// <summary>获取有效令牌；过期时重新请求。</summary>
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);

    /// <summary>使当前缓存失效（收到 401 时调用）。</summary>
    void Invalidate();
}
