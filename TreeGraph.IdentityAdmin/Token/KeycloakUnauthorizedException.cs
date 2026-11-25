namespace TreeGraph.IdentityAdmin.Token;

/// <summary>
/// 管理 API 在强制刷新令牌后仍返回 401 时抛出。
/// </summary>
public sealed class KeycloakUnauthorizedException : Exception
{
    public KeycloakUnauthorizedException(string message)
        : base(message)
    {
    }
}
