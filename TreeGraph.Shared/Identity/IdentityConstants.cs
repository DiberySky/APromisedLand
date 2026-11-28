namespace TreeGraph.Shared.Identity;

/// <summary>
/// 身份模型中的固定约定。
/// </summary>
public static class IdentityConstants
{
    public const string Realm = "apromisedland";
    public const string WasmClientId = "treegraph-wasm";

    /// <summary>唯一超级用户的固定登录名和重建 ID。</summary>
    public const string SuperuserUsername = "DiberySky";
    public const string SuperuserId = "40000000-0000-4000-8000-000000000001";

    /// <summary>开发环境管理员测试账号。</summary>
    public const string DevelopmentAdminUsername = "admin";
    public const string DevelopmentAdminId = "40000000-0000-4000-8000-000000000002";

    /// <summary>开发环境手机号普通用户测试账号。</summary>
    public const string DevelopmentUserUsername = "13908639757";
    public const string DevelopmentUserId = "40000000-0000-4000-8000-000000000003";

    /// <summary>空间组成员组名前缀，形如 <c>space-{spaceId}</c>。</summary>
    public const string SpaceGroupPrefix = "space-";

    /// <summary>所有空间组的父组路径。</summary>
    public const string SpacesGroupPath = "/spaces";

    /// <summary>管理台默认组路径。</summary>
    public const string DefaultGroupPath = "/diberysky";

    /// <summary>管理员组路径。</summary>
    public const string AdminGroupPath = "/Admin";

    /// <summary>管理台策略名：需要 app-admin。</summary>
    public const string AdminOnlyPolicy = "AdminOnly";

    /// <summary>管理令牌使用的 client 标识。</summary>
    public const string AdminClientId = "treegraph-admin";

    /// <summary>撤销标记在 Redis 中的键前缀，完整键为 <c>identity:revoked:{sub}</c>。</summary>
    public const string RevocationKeyPrefix = "identity:revoked:";
}
