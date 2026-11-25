using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using TreeGraph.Blazor.Infrastructure.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.Blazor.Infrastructure.Admin;

public sealed class UsersAdminApiClient(
    HttpClient http,
    WasmOidcSession session,
    NavigationManager navigation)
{
    public Task<PagedResult<IdentityUserSummary>?> SearchAsync(AdminPageRequest request, CancellationToken ct = default)
    {
        var query = $"?search={Uri.EscapeDataString(request.Search ?? string.Empty)}&page={request.NormalizedPage}&pageSize={request.NormalizedPageSize}";
        if (request.Enabled.HasValue)
            query += $"&enabled={request.Enabled.Value.ToString().ToLowerInvariant()}";
        return ReadAsync<PagedResult<IdentityUserSummary>>("api/admin/users" + query, ct);
    }

    public Task<IdentityUserDetail?> GetAsync(string userId, CancellationToken ct = default)
        => ReadAsync<IdentityUserDetail>($"api/admin/users/{Uri.EscapeDataString(userId)}", ct);

    public Task<AdminResult<DisableUserResult>> DisableAsync(string userId, CancellationToken ct = default)
        => WriteAsync<DisableUserResult>(userId, "disable", ct);

    public Task<AdminResult> EnableAsync(string userId, CancellationToken ct = default)
        => WriteAsync(userId, "enable", ct);

    private async Task<T?> ReadAsync<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;
        if (!response.IsSuccessStatusCode)
            throw new AdminApiException(response.StatusCode, await ReadErrorAsync(response, ct));
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    private async Task<AdminResult> WriteAsync(string userId, string action, CancellationToken ct)
    {
        using var response = await http.PostAsync($"api/admin/users/{Uri.EscapeDataString(userId)}/{action}", null, ct);
        if (response.StatusCode == (HttpStatusCode)428)
        {
            await BeginStepUpAsync();
            throw new StepUpRequiredException();
        }

        var result = await TryReadAsync<AdminResult>(response, ct);
        if (result is not null)
            return result;
        if (!response.IsSuccessStatusCode)
            throw new AdminApiException(response.StatusCode, await ReadErrorAsync(response, ct));
        throw new AdminApiException(response.StatusCode, "管理 API 返回了空响应。");
    }

    private async Task<AdminResult<T>> WriteAsync<T>(string userId, string action, CancellationToken ct)
    {
        using var response = await http.PostAsync($"api/admin/users/{Uri.EscapeDataString(userId)}/{action}", null, ct);
        if (response.StatusCode == (HttpStatusCode)428)
        {
            await BeginStepUpAsync();
            throw new StepUpRequiredException();
        }

        var result = await TryReadAsync<AdminResult<T>>(response, ct);
        if (result is not null)
            return result;
        if (!response.IsSuccessStatusCode)
            throw new AdminApiException(response.StatusCode, await ReadErrorAsync(response, ct));
        throw new AdminApiException(response.StatusCode, "管理 API 返回了空响应。");
    }

    private Task BeginStepUpAsync()
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        return session.BeginLoginAsync(string.IsNullOrEmpty(relative) ? "/" : "/" + relative, forceReauthentication: true);
    }

    private static async Task<T?> TryReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return default;
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrWhiteSpace(body)
                ? $"管理 API 请求失败（HTTP {(int)response.StatusCode}）。"
                : body;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }
}

public class AdminApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class StepUpRequiredException() : Exception("需要重新认证后再执行此操作。");
