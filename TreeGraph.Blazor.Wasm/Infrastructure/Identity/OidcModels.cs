using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using TreeGraph.Shared.Identity;

namespace TreeGraph.Blazor.Infrastructure.Identity;

public sealed class OidcTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
    [JsonPropertyName("id_token")] public string? IdToken { get; init; }
    [JsonPropertyName("token_type")] public string? TokenType { get; init; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    [JsonPropertyName("refresh_expires_in")] public int RefreshExpiresIn { get; init; }
}

public sealed class PendingOidcLogin
{
    public string CodeVerifier { get; init; } = string.Empty;
    public string Nonce { get; init; } = string.Empty;
    public string ReturnUrl { get; init; } = "/";
}

internal sealed record BrowserTokenSet(
    string AccessToken,
    string? RefreshToken,
    string? IdToken,
    DateTimeOffset AccessTokenExpiresAt,
    ClaimsPrincipal Principal);

public static class BrowserJwt
{
    public static ClaimsPrincipal CreatePrincipal(string idToken, string accessToken, string authority, string clientId, string nonce)
    {
        var identityPayload = ReadPayload(idToken);
        ValidateIdentityToken(identityPayload, authority, clientId, nonce);
        var accessPayload = ReadPayload(accessToken);
        var claims = new List<Claim>();
        AddClaims(claims, identityPayload);
        AddClaims(claims, accessPayload);
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "Keycloak OIDC",
            nameType: IdentityClaimTypes.PreferredUsername,
            roleType: IdentityClaimTypes.Roles));
    }

    public static DateTimeOffset ReadExpiration(string token)
    {
        var payload = ReadPayload(token);
        if (!payload.TryGetProperty("exp", out var exp) || !exp.TryGetInt64(out var seconds))
            throw new InvalidOperationException("令牌缺少有效 exp。");
        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    private static void ValidateIdentityToken(JsonElement payload, string authority, string clientId, string nonce)
    {
        if (!payload.TryGetProperty("nonce", out var nonceElement)
            || !string.Equals(nonceElement.GetString(), nonce, StringComparison.Ordinal))
            throw new InvalidOperationException("OIDC nonce 校验失败。");
        if (!payload.TryGetProperty("iss", out var issuer)
            || !string.Equals(issuer.GetString()?.TrimEnd('/'), authority.TrimEnd('/'), StringComparison.Ordinal))
            throw new InvalidOperationException("OIDC issuer 校验失败。");
        var validAudience = payload.TryGetProperty("aud", out var audience)
            && (audience.ValueKind == JsonValueKind.String
                ? audience.GetString() == clientId
                : audience.ValueKind == JsonValueKind.Array
                  && audience.EnumerateArray().Any(x => x.GetString() == clientId));
        if (!validAudience
            && (!payload.TryGetProperty("azp", out var azp) || azp.GetString() != clientId))
            throw new InvalidOperationException("OIDC audience 校验失败。");
        if (!payload.TryGetProperty("exp", out var exp) || !exp.TryGetInt64(out var seconds)
            || DateTimeOffset.FromUnixTimeSeconds(seconds) <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("OIDC 身份令牌已过期或缺少 exp。");
    }

    private static void AddClaims(List<Claim> claims, JsonElement payload)
    {
        if (payload.TryGetProperty("realm_access", out var realmAccess)
            && realmAccess.ValueKind == JsonValueKind.Object
            && realmAccess.TryGetProperty("roles", out var realmRoles))
            AddClaimValues(claims, IdentityClaimTypes.Roles, realmRoles);

        foreach (var property in payload.EnumerateObject())
        {
            if (property.NameEquals("realm_access")) continue;
            var type = property.Name;
            AddClaimValues(claims, type, property.Value);
        }
    }

    private static void AddClaimValues(List<Claim> claims, string type, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                AddClaimValues(claims, type, item);
            return;
        }
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.Object) return;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        if (!string.IsNullOrEmpty(text)
            && !claims.Any(c => c.Type == type && c.Value == text))
            claims.Add(new Claim(type, text));
    }

    private static JsonElement ReadPayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) throw new InvalidOperationException("OIDC 返回了非 JWT 令牌。");
        var base64 = parts[1].Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        using var document = JsonDocument.Parse(Convert.FromBase64String(base64));
        return document.RootElement.Clone();
    }
}
