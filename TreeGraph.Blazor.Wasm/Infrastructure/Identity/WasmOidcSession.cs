using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using TreeGraph.Blazor.Configuration;

namespace TreeGraph.Blazor.Infrastructure.Identity;

public sealed class WasmOidcSession : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WasmRuntimeConfig _config;
    private readonly NavigationManager _navigation;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private IJSObjectReference? _module;
    private DotNetObjectReference<WasmOidcSession>? _selfReference;
    private BrowserTokenSet? _tokens;
    private bool _initialized;
    private bool _isSigningOut;

    public WasmOidcSession(IJSRuntime js, IHttpClientFactory httpClientFactory, WasmRuntimeConfig config, NavigationManager navigation)
    {
        _js = js;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _navigation = navigation;
    }

    public event Action? AuthenticationChanged;
    public AuthenticationState CurrentAuthenticationState => new(_tokens?.Principal ?? new ClaimsPrincipal(new ClaimsIdentity()));
    public bool IsAuthenticated => _tokens?.Principal.Identity?.IsAuthenticated == true;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        var module = await GetModuleAsync();
        _selfReference = DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("watchLogout", _selfReference);
        _ = RefreshLoopAsync(_disposeCts.Token);

        var relative = _navigation.ToBaseRelativePath(_navigation.Uri);
        if (relative.StartsWith("authentication/login-callback", StringComparison.OrdinalIgnoreCase)) return;
        if (!IsAuthenticated) await BeginLoginAsync(LocalReturnUrl());
    }

    public async Task BeginLoginAsync(string? returnUrl = null, bool forceReauthentication = false)
    {
        if (_isSigningOut) return;
        await _loginLock.WaitAsync();
        try
        {
            if (_isSigningOut) return;
            var module = await GetModuleAsync();
            if (!forceReauthentication && await module.InvokeAsync<bool>("hasPendingLogin")) return;
            var redirectUri = _navigation.BaseUri + "authentication/login-callback";
            await module.InvokeVoidAsync(
                "beginLogin",
                _config.Keycloak.Authority.TrimEnd('/'),
                _config.Keycloak.ClientId,
                redirectUri,
                NormalizeReturnUrl(returnUrl),
                forceReauthentication ? 0 : (int?)null);
        }
        finally
        {
            _loginLock.Release();
        }
    }

    public async Task<string> CompleteLoginAsync()
    {
        var query = ParseQuery(_navigation.Uri);
        if (query.TryGetValue("error", out var error))
            throw new InvalidOperationException($"身份提供方拒绝登录：{error}");
        if (!query.TryGetValue("code", out var code) || !query.TryGetValue("state", out var state))
            throw new InvalidOperationException("OIDC 回调缺少 code 或 state。");

        var module = await GetModuleAsync();
        var pending = await module.InvokeAsync<PendingOidcLogin?>("consumeLoginTransaction", state)
            ?? throw new InvalidOperationException("OIDC state 校验失败或登录事务已失效。");
        var client = _httpClientFactory.CreateClient("oidc");
        var response = await client.PostAsync(
            TokenEndpoint(),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _config.Keycloak.ClientId,
                ["code"] = code,
                ["code_verifier"] = pending.CodeVerifier,
                ["redirect_uri"] = _navigation.BaseUri + "authentication/login-callback"
            }));
        var tokens = await ReadTokenResponseAsync(response);
        ApplyTokens(tokens, pending.Nonce, requireIdentityToken: true);
        return NormalizeReturnUrl(pending.ReturnUrl);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_tokens is { } current && current.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return current.AccessToken;
        return await RefreshAsync(cancellationToken);
    }

    public async Task<string?> GetAccessTokenAfterUnauthorizedAsync(string failedToken, CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_tokens is { } current
                && current.AccessToken != failedToken
                && current.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return current.AccessToken;
            return await RefreshCoreAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task SignOutAsync()
    {
        if (_isSigningOut) return;
        _isSigningOut = true;
        try
        {
            var idToken = _tokens?.IdToken;
            var module = await GetModuleAsync();
            ClearLocalSession();
            await module.InvokeVoidAsync("broadcastLogout");
            await module.InvokeVoidAsync(
                "endLogout",
                _config.Keycloak.Authority.TrimEnd('/'),
                idToken,
                _navigation.BaseUri);
        }
        catch
        {
            _isSigningOut = false;
            throw;
        }
    }

    [JSInvokable]
    public Task OnRemoteLogout()
    {
        _isSigningOut = true;
        ClearLocalSession();
        _navigation.NavigateTo("/");
        return Task.CompletedTask;
    }

    private async Task<string> RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_tokens is { } current && current.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return current.AccessToken;
            return await RefreshCoreAsync(cancellationToken)
                ?? throw new InvalidOperationException("登录会话已过期，请重新登录。");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<string?> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var refreshToken = _tokens?.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken)) return null;
        var client = _httpClientFactory.CreateClient("oidc");
        using var response = await client.PostAsync(
            TokenEndpoint(),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _config.Keycloak.ClientId,
                ["refresh_token"] = refreshToken
            }), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ClearLocalSession();
            return null;
        }
        var tokens = await response.Content.ReadFromJsonAsync<OidcTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("令牌刷新响应为空。");
        ApplyTokens(tokens, expectedNonce: null, requireIdentityToken: false);
        return _tokens?.AccessToken;
    }

    private void ApplyTokens(OidcTokenResponse response, string? expectedNonce, bool requireIdentityToken)
    {
        if (string.IsNullOrWhiteSpace(response.AccessToken)
            || !string.Equals(response.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("令牌端点未返回 Bearer access token。");
        var idToken = response.IdToken ?? _tokens?.IdToken;
        if (idToken is null && requireIdentityToken)
            throw new InvalidOperationException("令牌端点未返回 id token。");
        if (idToken is null) throw new InvalidOperationException("当前会话缺少 id token。");
        if (expectedNonce is not null)
        {
            var principal = BrowserJwt.CreatePrincipal(idToken, response.AccessToken, _config.Keycloak.Authority, _config.Keycloak.ClientId, expectedNonce);
            _tokens = new BrowserTokenSet(
                response.AccessToken,
                response.RefreshToken ?? _tokens?.RefreshToken,
                idToken,
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, response.ExpiresIn)),
                principal);
        }
        else
        {
            var principal = _tokens?.Principal
                ?? throw new InvalidOperationException("无法刷新未初始化的会话。");
            _tokens = new BrowserTokenSet(
                response.AccessToken,
                response.RefreshToken ?? _tokens?.RefreshToken,
                idToken,
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, response.ExpiresIn)),
                principal);
        }
        AuthenticationChanged?.Invoke();
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_tokens is null || _tokens.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) continue;
                _ = await RefreshAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { ClearLocalSession(); }
    }

    private void ClearLocalSession()
    {
        _tokens = null;
        AuthenticationChanged?.Invoke();
    }

    private async Task<IJSObjectReference> GetModuleAsync()
        => _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/oidc.js");

    private static async Task<OidcTokenResponse> ReadTokenResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"令牌交换失败（HTTP {(int)response.StatusCode}）。");
        return await response.Content.ReadFromJsonAsync<OidcTokenResponse>()
            ?? throw new InvalidOperationException("令牌端点返回空响应。");
    }

    private string TokenEndpoint() => _config.Keycloak.Authority.TrimEnd('/') + "/protocol/openid-connect/token";

    private string LocalReturnUrl()
    {
        var relative = _navigation.ToBaseRelativePath(_navigation.Uri);
        return string.IsNullOrEmpty(relative) ? "/" : "/" + relative;
    }

    private static string NormalizeReturnUrl(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/";

    private static Dictionary<string, string> ParseQuery(string uri)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var question = uri.IndexOf('?');
        if (question < 0) return result;
        foreach (var pair in uri[(question + 1)..].Split('#')[0].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Decode(separator < 0 ? pair : pair[..separator]);
            result[key] = Decode(separator < 0 ? string.Empty : pair[(separator + 1)..]);
        }
        return result;
        static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
    }

    public async ValueTask DisposeAsync()
    {
        await _disposeCts.CancelAsync();
        _disposeCts.Dispose();
        _selfReference?.Dispose();
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("unwatchLogout");
            await _module.DisposeAsync();
        }
        _refreshLock.Dispose();
        _loginLock.Dispose();
    }
}
