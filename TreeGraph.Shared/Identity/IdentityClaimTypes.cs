namespace TreeGraph.Shared.Identity;

/// <summary>
/// Keycloak 令牌中被本系统消费的 claim 名称。
/// 与 realm protocol mapper 配置保持一致（roles / groups 分别独立输出）。
/// </summary>
public static class IdentityClaimTypes
{
    /// <summary>主体标识。</summary>
    public const string Subject = "sub";

    /// <summary>realm 角色列表，对应 roles mapper。</summary>
    public const string Roles = "roles";

    /// <summary>组列表，对应 groups mapper（full.path=false）。</summary>
    public const string Groups = "groups";

    /// <summary>登录时间（Unix 秒），用于 step-up 新鲜度判断。</summary>
    public const string AuthTime = "auth_time";

    /// <summary>用户名。</summary>
    public const string PreferredUsername = "preferred_username";

    /// <summary>邮箱。</summary>
    public const string Email = "email";

    /// <summary>显示名。</summary>
    public const string Name = "name";
}
