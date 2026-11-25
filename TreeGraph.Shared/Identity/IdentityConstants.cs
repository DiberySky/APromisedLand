namespace TreeGraph.Shared.Identity;

/// <summary>
/// 身份模型中的固定约定。
/// </summary>
public static class IdentityConstants
{
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
