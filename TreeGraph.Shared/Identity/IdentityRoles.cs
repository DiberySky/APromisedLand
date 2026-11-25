namespace TreeGraph.Shared.Identity;

/// <summary>
/// 本系统使用的 realm 角色。仅使用 realm roles，不引入 client roles 作为业务权限。
/// </summary>
public static class IdentityRoles
{
    /// <summary>管理台入口与高危操作。</summary>
    public const string Admin = "app-admin";

    /// <summary>业务写权限。</summary>
    public const string Editor = "app-editor";

    /// <summary>业务只读。</summary>
    public const string Viewer = "app-viewer";

    /// <summary>
    /// Admin 角色名别名集合：同时接受短横线和下划线两种拼写。
    /// Keycloak 历史数据中可能存在下划线版本，代码统一容忍。
    /// </summary>
    public static IReadOnlySet<string> AdminAliases { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Admin,
        "app_admin",
    };

    /// <summary>检查用户角色集合中是否包含任意一个 Admin 别名。</summary>
    public static bool HasAdminRole(IReadOnlyList<string> roles)
        => roles.Any(r => AdminAliases.Contains(r));
}
