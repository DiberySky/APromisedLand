namespace TreeGraph.Shared.Identity;

/// <summary>
/// 本系统使用的 realm 角色。仅使用 realm roles，不引入 client roles 作为业务权限。
/// </summary>
public static class IdentityRoles
{
    /// <summary>唯一超级用户权限。</summary>
    public const string Superuser = "app-superuser";

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
    public static bool HasAdminRole(IEnumerable<string> roles)
        => roles.Any(r => AdminAliases.Contains(r));

    public static bool HasSuperuserRole(IEnumerable<string> roles)
        => roles.Contains(Superuser, StringComparer.Ordinal);

    /// <summary>角色中文显示名；权限判断仍只使用英文内部名。</summary>
    public static string GetDisplayName(string roleName) => roleName switch
    {
        Superuser => "超级用户",
        Admin or "app_admin" => "管理员",
        Editor => "编辑用户",
        Viewer => "普通用户",
        _ => roleName
    };
}
