namespace TreeGraph.Shared.Identity.Admin;

/// <summary>用户列表项。</summary>
public sealed record IdentityUserSummary(
    string Id,
    string Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    bool EmailVerified,
    long? CreatedTimestamp);

/// <summary>用户详情。</summary>
public sealed record IdentityUserDetail(
    string Id,
    string Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    bool EmailVerified,
    long? CreatedTimestamp,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> RequiredActions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Attributes);

/// <summary>创建用户请求。</summary>
public sealed record CreateUserRequest(
    string Username,
    string? Email = null,
    string? FirstName = null,
    string? LastName = null,
    bool Enabled = true,
    bool EmailVerified = false,
    string? TemporaryPassword = null,
    bool RequirePasswordChange = true,
    IReadOnlyList<string>? GroupPaths = null,
    IReadOnlyList<string>? RealmRoles = null);

/// <summary>更新用户基础资料请求。null 表示不修改。</summary>
public sealed record UpdateUserRequest(
    string? Email = null,
    string? FirstName = null,
    string? LastName = null,
    bool? EmailVerified = null);

/// <summary>重置密码请求。</summary>
public sealed record ResetPasswordRequest(string Password, bool Temporary);

/// <summary>禁用用户阶段性结果。</summary>
public sealed record DisableUserResult(
    bool Disabled,
    bool SessionsRevoked,
    bool RevocationMarked,
    bool Retryable);

/// <summary>用户管理服务。</summary>
public interface IUsersAdminService
{
    Task<PagedResult<IdentityUserSummary>> SearchAsync(AdminPageRequest request, CancellationToken ct = default);

    Task<IdentityUserDetail?> GetAsync(string userId, CancellationToken ct = default);

    Task<AdminResult<string>> CreateAsync(AdminActor actor, CreateUserRequest request, CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(AdminActor actor, string userId, UpdateUserRequest request, CancellationToken ct = default);

    Task<AdminResult> ResetPasswordAsync(AdminActor actor, string userId, ResetPasswordRequest request, CancellationToken ct = default);

    /// <summary>禁用用户：禁用 → 吊销会话 → 写撤销标记（幂等 saga）。</summary>
    Task<AdminResult<DisableUserResult>> DisableAsync(AdminActor actor, string userId, CancellationToken ct = default);

    /// <summary>启用用户：启用 → 清除撤销标记。</summary>
    Task<AdminResult> EnableAsync(AdminActor actor, string userId, CancellationToken ct = default);

    /// <summary>仅吊销该用户全部会话（用于部分成功后的重试）。</summary>
    Task<AdminResult> LogoutSessionsAsync(AdminActor actor, string userId, CancellationToken ct = default);
}
