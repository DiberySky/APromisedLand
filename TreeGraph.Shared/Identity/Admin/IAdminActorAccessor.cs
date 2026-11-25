using System.Security.Claims;

namespace TreeGraph.Shared.Identity.Admin;

/// <summary>
/// 从当前请求的 <see cref="ClaimsPrincipal"/> 构造 <see cref="AdminActor"/>。
/// 实现不得接受前端提交的身份字段。
/// </summary>
public interface IAdminActorAccessor
{
    /// <summary>获取当前管理员；未认证时抛出异常。</summary>
    AdminActor GetRequiredActor();

    /// <summary>尝试获取当前管理员；未认证时返回 null。</summary>
    AdminActor? TryGetActor();
}
