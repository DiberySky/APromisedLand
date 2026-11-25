using System.Security.Claims;

namespace TreeGraph.Shared.Identity.Admin;

/// <summary>二次认证（step-up）状态。</summary>
public sealed record StepUpState(bool IsFresh, DateTimeOffset? AuthTime, int MaxAgeSeconds);

/// <summary>一次性操作票据。</summary>
public sealed record StepUpTicket(string Token, string Action, string? ReturnUrl, DateTimeOffset ExpiresAt);

/// <summary>
/// 高危操作的二次认证。实现基于 auth_time 新鲜度与 Redis 一次性票据。
/// </summary>
public interface IStepUpService
{
    /// <summary>当前主体是否满足二次认证新鲜度。</summary>
    StepUpState GetState(ClaimsPrincipal principal);

    /// <summary>把待执行操作写入一次性票据，返回票据标识。</summary>
    Task<StepUpTicket> CreateTicketAsync(AdminActor actor, string action, string? payload, string? returnUrl, CancellationToken ct = default);

    /// <summary>消费票据；票据不存在、已过期或 actor 不匹配时返回 null。</summary>
    Task<StepUpTicket?> ConsumeTicketAsync(AdminActor actor, string token, CancellationToken ct = default);
}
