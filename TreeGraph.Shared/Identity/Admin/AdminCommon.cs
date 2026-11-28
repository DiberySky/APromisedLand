namespace TreeGraph.Shared.Identity.Admin;

/// <summary>
/// 管理操作的真实操作者。所有写方法都必须由服务端从 <see cref="System.Security.Claims.ClaimsPrincipal"/>
/// 构造并显式传入，禁止由前端提交。
/// </summary>
public sealed record AdminActor(string Sub, string Username, IReadOnlyList<string> Roles)
{
    /// <summary>是否具备超级用户角色。</summary>
    public bool IsSuperuser => IdentityRoles.HasSuperuserRole(Roles);

    /// <summary>是否可进入管理台；超级用户包含管理员权限。</summary>
    public bool IsAdmin => IsSuperuser || IdentityRoles.HasAdminRole(Roles);
}

/// <summary>无返回值的操作结果。</summary>
public sealed record AdminResult(bool Success, string? Error = null, int? StatusCode = null)
{
    public static AdminResult Ok() => new(true);

    public static AdminResult Fail(string error, int? statusCode = null) => new(false, error, statusCode);
}

/// <summary>带返回值的操作结果。</summary>
public sealed record AdminResult<T>(bool Success, T? Value, string? Error = null, int? StatusCode = null)
{
    public static AdminResult<T> Ok(T value) => new(true, value);

    public static AdminResult<T> Fail(string error, int? statusCode = null) => new(false, default, error, statusCode);
}

/// <summary>分页结果。</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (Total + PageSize - 1) / PageSize;

    public static PagedResult<T> Empty(int page = 1, int pageSize = 25) => new([], 0, page, pageSize);
}

/// <summary>分页请求。Keycloak 采用 first/max 语义，<see cref="First"/> 负责换算。</summary>
public sealed record AdminPageRequest(string? Search = null, int Page = 1, int PageSize = 25, bool? Enabled = null)
{
    /// <summary>规范化后的页码（从 1 开始）。</summary>
    public int NormalizedPage => Page < 1 ? 1 : Page;

    /// <summary>规范化后的页大小（1..200）。</summary>
    public int NormalizedPageSize => PageSize is < 1 or > 200 ? 25 : PageSize;

    /// <summary>对应 Keycloak 的 first 参数。</summary>
    public int First => (NormalizedPage - 1) * NormalizedPageSize;

    /// <summary>对应 Keycloak 的 max 参数。</summary>
    public int Max => NormalizedPageSize;
}
