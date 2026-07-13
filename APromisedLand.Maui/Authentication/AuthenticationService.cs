using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using APromisedLand.Shared.Interfaces;
using APromisedLand.Shared.Services;
using SolutionService = APromisedLand.Shared.Services.Solution.SolutionService;

namespace APromisedLand.Maui.Authentication;

public class AuthenticationService(HttpClient httpClient, 
    JwtAuthenticationStateProvider authStateProvider)
    : IAuthenticationService
{
    // AuthClient

    public async Task<bool> LoginAsync(string username, string password)
    {
        var requestUri = $"/realms/{SolutionService.Realm}/protocol/openid-connect/token";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = SolutionService.ClientId,
            ["username"] = username,
            ["password"] = password
        });

        var response = await httpClient.PostAsync(requestUri, content);
        if (!response.IsSuccessStatusCode)
        {
            // 1. 获取状态码和原因短语
            var statusCode = (int)response.StatusCode;
            var reasonPhrase = response.ReasonPhrase;

            // 2. 读取响应体内容（注意：Content 可能为 null，或者只能读取一次）
            string errorBody = await response.Content.ReadAsStringAsync();

            // 3. 可以将这些信息整合为错误消息，例如：
            var errorMessage = $"HTTP {statusCode} ({reasonPhrase}) - 响应内容：{errorBody}";

            throw new Exception(errorMessage);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        var refreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        if (string.IsNullOrEmpty(accessToken)) return false;

        await authStateProvider.MarkUserAsAuthenticated(accessToken, refreshToken);
        return true;
    }

    public async Task LogoutAsync()
    {
        await authStateProvider.MarkUserAsLoggedOut();
    }
}