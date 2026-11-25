using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.Blazor.Infrastructure.Admin;

/// <summary>
/// 共享管理页面的浏览器占位。HTTP 代理不会序列化或使用该值；
/// 真正的 AdminActor 只能由 IdentityAdmin.Api 从当前 JWT claims 构造。
/// </summary>
public sealed class WasmAdminActorAccessor : IAdminActorAccessor
{
    public AdminActor GetRequiredActor() => null!;

    public AdminActor? TryGetActor() => null;
}
