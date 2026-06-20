using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace APromisedLand.Maui.Authentication;

public class JwtAuthorizationMessageHandler : DelegatingHandler
{
    private readonly ISecureStorage _secureStorage;
    private readonly JwtAuthenticationStateProvider _authStateProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public JwtAuthorizationMessageHandler(
        ISecureStorage secureStorage,
        JwtAuthenticationStateProvider authStateProvider,
        IHttpClientFactory httpClientFactory)
    {
        _secureStorage = secureStorage;
        _authStateProvider = authStateProvider;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. 从存储中附加当前 access_token
        var token = await _secureStorage.GetAsync(StorageKeys.AccessToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // 2. 发送请求
        var response = await base.SendAsync(request, cancellationToken);

        // 3. 如果不是 401，直接返回
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // 4. 收到 401，尝试刷新令牌
        var newToken = await TryRefreshTokenAsync(cancellationToken);
        if (newToken == null)
        {
            // 刷新失败，登出并原样返回 401
            await _authStateProvider.MarkUserAsLoggedOut();
            return response;
        }

        // 5. 用新令牌重试原始请求（注意：需要克隆请求，因为原请求已发送）
        var retryRequest = await CloneHttpRequestMessageAsync(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private async Task<string?> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        // 并发控制：只允许一个刷新操作
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // 再次检查当前存储的 access_token，防止重复刷新
            var currentAccess = await _secureStorage.GetAsync(StorageKeys.AccessToken);
            // 如果已经在刷新期间被其他线程更新，且新令牌与当前不同，直接返回新令牌
            var refreshToken = await _secureStorage.GetAsync(StorageKeys.RefreshToken);
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            // 调用 Keycloak 刷新端点
            var refreshClient = _httpClientFactory.CreateClient("RefreshClient");
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "refresh_token",
                ["client_id"]     = "diberyclient",
                ["refresh_token"] = refreshToken
            });

            var response = await refreshClient.PostAsync(
                "/realms/diberysky/protocol/openid-connect/token", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var newAccessToken = json.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var newRefreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            if (string.IsNullOrEmpty(newAccessToken))
                return null;

            // 更新存储
            await _secureStorage.SetAsync(StorageKeys.AccessToken, newAccessToken);
            if (!string.IsNullOrEmpty(newRefreshToken))
                await _secureStorage.SetAsync(StorageKeys.RefreshToken, newRefreshToken);

            // 通知认证状态变更（重新从存储读取，构建用户）
            //var authState = await _authStateProvider.GetAuthenticationStateAsync();
            await _authStateProvider.NotifyStateChanged();

            return newAccessToken;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[JwtHandler] 令牌刷新失败：{e.Message}");
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // 克隆 HttpRequestMessage，因为原始请求已发送，不能直接重用
    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content != null)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);
            if (request.Content.Headers != null)
                foreach (var h in request.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
}
