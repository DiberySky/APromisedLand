using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.StepUp;

/// <summary>
/// step-up 实现：auth_time 新鲜度判定 + Redis 一次性票据。
/// </summary>
public sealed class StepUpService : IStepUpService
{
    private const string KeyPrefix = "identity:stepup:";
    private static readonly TimeSpan TicketTtl = TimeSpan.FromMinutes(5);

    private readonly IDistributedCache _cache;
    private readonly IOptionsMonitor<KeycloakAdminOptions> _options;
    private readonly TimeProvider _timeProvider;

    public StepUpService(
        IDistributedCache cache,
        IOptionsMonitor<KeycloakAdminOptions> options,
        TimeProvider timeProvider)
    {
        _cache = cache;
        _options = options;
        _timeProvider = timeProvider;
    }

    public StepUpState GetState(ClaimsPrincipal principal)
    {
        var maxAge = _options.CurrentValue.StepUpMaxAgeSeconds;
        var raw = principal.FindFirst(IdentityClaimTypes.AuthTime)?.Value
                  ?? principal.FindFirst("auth_time")?.Value;

        if (string.IsNullOrWhiteSpace(raw) || !long.TryParse(raw, out var seconds))
            return new StepUpState(false, null, maxAge);

        var authTime = DateTimeOffset.FromUnixTimeSeconds(seconds);
        var fresh = _timeProvider.GetUtcNow() - authTime <= TimeSpan.FromSeconds(maxAge);
        return new StepUpState(fresh, authTime, maxAge);
    }

    public async Task<StepUpTicket> CreateTicketAsync(
        AdminActor actor,
        string action,
        string? payload,
        string? returnUrl,
        CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAt = _timeProvider.GetUtcNow().Add(TicketTtl);

        var ticket = new StoredTicket(actor.Sub, action, payload, returnUrl, expiresAt);
        var json = JsonSerializer.Serialize(ticket);

        await _cache.SetStringAsync(
                KeyPrefix + token,
                json,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TicketTtl },
                ct)
            .ConfigureAwait(false);

        return new StepUpTicket(token, action, returnUrl, expiresAt);
    }

    public async Task<StepUpTicket?> ConsumeTicketAsync(
        AdminActor actor,
        string token,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var key = KeyPrefix + token;
        var json = await _cache.GetStringAsync(key, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        await _cache.RemoveAsync(key, ct).ConfigureAwait(false);

        var stored = JsonSerializer.Deserialize<StoredTicket>(json);
        if (stored is null || !string.Equals(stored.Sub, actor.Sub, StringComparison.Ordinal))
            return null;

        if (stored.ExpiresAt <= _timeProvider.GetUtcNow())
            return null;

        return new StepUpTicket(token, stored.Action, stored.ReturnUrl, stored.ExpiresAt);
    }

    private sealed record StoredTicket(
        string Sub,
        string Action,
        string? Payload,
        string? ReturnUrl,
        DateTimeOffset ExpiresAt);
}
