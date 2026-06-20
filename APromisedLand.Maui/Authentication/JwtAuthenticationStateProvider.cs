using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace APromisedLand.Maui.Authentication;

public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ISecureStorage _secureStorage;

    public JwtAuthenticationStateProvider(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _secureStorage.GetAsync(StorageKeys.AccessToken);
        if (string.IsNullOrEmpty(token))
            return AnonymousState;

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            await MarkUserAsLoggedOut();
            return AnonymousState;
        }

        var jwt = handler.ReadJwtToken(token);
        if (jwt.ValidTo < DateTime.UtcNow)
        {
            // 令牌过期，不立即登出，交给 DelegatingHandler 尝试刷新
            // 这里仍然返回未认证状态，但不清除令牌，以便刷新时仍可获取 refresh_token
            return AnonymousState;
        }

        var claims = jwt.Claims.ToList();

        // Keycloak → .NET 标准映射
        MapClaim(claims, "given_name", ClaimTypes.GivenName);
        MapClaim(claims, "family_name", ClaimTypes.Surname);
        MapClaim(claims, "preferred_username", ClaimTypes.Name);
        MapClaim(claims, "email", ClaimTypes.Email);

        var identity = new ClaimsIdentity(jwt.Claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task MarkUserAsAuthenticated(string accessToken, string? refreshToken = null)
    {
        await _secureStorage.SetAsync(StorageKeys.AccessToken, accessToken);
        if (!string.IsNullOrEmpty(refreshToken))
            await _secureStorage.SetAsync(StorageKeys.RefreshToken, refreshToken);

        var authState = await GetAuthenticationStateAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(authState));
    }

    public async Task MarkUserAsLoggedOut()
    {
        _secureStorage.Remove(StorageKeys.AccessToken);
        _secureStorage.Remove(StorageKeys.RefreshToken);
        NotifyAuthenticationStateChanged(Task.FromResult(AnonymousState));
    }

    public async Task NotifyStateChanged()
    {
        var authState = await GetAuthenticationStateAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(authState));
    }

    private void MapClaim(List<Claim> claims, string source, string target)
    {
        var c = claims.FirstOrDefault(x => x.Type == source);
        if (c != null)
            claims.Add(new Claim(target, c.Value));
    }

    private static AuthenticationState AnonymousState
        => new(new ClaimsPrincipal(new ClaimsIdentity()));
}
