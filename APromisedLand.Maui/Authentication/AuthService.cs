using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Intefaces;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace APromisedLand.Maui.Authentication;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;       // AuthClient
    private readonly JwtAuthenticationStateProvider _authStateProvider;

    public AuthService(HttpClient httpClient, JwtAuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _authStateProvider = authStateProvider;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var requestUri = $"/realms/{AuthHelper.Realm}/protocol/openid-connect/token";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"]  = AuthHelper.ClientId,
            ["username"]   = username,
            ["password"]   = password
        });

        var response = await _httpClient.PostAsync(requestUri, content);
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        var refreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        if (string.IsNullOrEmpty(accessToken)) return false;

        await _authStateProvider.MarkUserAsAuthenticated(accessToken, refreshToken);
        return true;
    }

    public async Task LogoutAsync()
    {
        await _authStateProvider.MarkUserAsLoggedOut();
    }
}
