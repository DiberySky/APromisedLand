using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using TreeGraph.Blazor.Infrastructure.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.Blazor.Infrastructure.Admin;

public sealed partial class IdentityAdminApiClient(
    HttpClient http,
    WasmOidcSession session,
    NavigationManager navigation) :
    IUsersAdminService,
    IDashboardAdminService,
    IGroupsAdminService,
    IRolesAdminService,
    IClientsAdminService,
    IClientScopesAdminService,
    IIdentityProvidersAdminService,
    IFederationAdminService,
    IAuthFlowsAdminService,
    IRealmSettingsAdminService,
    IRealmKeysAdminService,
    IOrganizationsAdminService,
    IEventsAdminService,
    ISessionsAdminService,
    IClientLogoutAdminService,
    IExportAdminService,
    IAdminAuditQueryService
{
    private const string Root = "api/admin/";

    public Task<PagedResult<IdentityUserSummary>> SearchAsync(AdminPageRequest request, CancellationToken ct = default)
        => GetRequiredAsync<PagedResult<IdentityUserSummary>>("users" + PageQuery(request), ct);

    public Task<IdentityUserDetail?> GetAsync(string userId, CancellationToken ct = default)
        => GetOrDefaultAsync<IdentityUserDetail>("users/" + Id(userId), ct);

    public Task<AdminResult<string>> CreateAsync(AdminActor actor, CreateUserRequest request, CancellationToken ct = default)
        => SendResultAsync<string>(HttpMethod.Post, "users", request, ct);

    public Task<AdminResult> UpdateAsync(AdminActor actor, string userId, UpdateUserRequest request, CancellationToken ct = default)
        => SendResultAsync(HttpMethod.Put, "users/" + Id(userId), request, ct);

    public Task<AdminResult> ResetPasswordAsync(AdminActor actor, string userId, ResetPasswordRequest request, CancellationToken ct = default)
        => SendResultAsync(HttpMethod.Post, "users/" + Id(userId) + "/reset-password", request, ct);

    public Task<AdminResult<DisableUserResult>> DisableAsync(AdminActor actor, string userId, CancellationToken ct = default)
        => SendResultAsync<DisableUserResult>(HttpMethod.Post, "users/" + Id(userId) + "/disable", null, ct);

    public Task<AdminResult> EnableAsync(AdminActor actor, string userId, CancellationToken ct = default)
        => SendResultAsync(HttpMethod.Post, "users/" + Id(userId) + "/enable", null, ct);

    public Task<AdminResult> LogoutSessionsAsync(AdminActor actor, string userId, CancellationToken ct = default)
        => SendResultAsync(HttpMethod.Post, "users/" + Id(userId) + "/sessions/logout", null, ct);

    public Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
        => GetRequiredAsync<DashboardSummary>("dashboard/summary", ct);

    public async Task<(string? Version, bool MatchesExpected)> GetVersionAsync(CancellationToken ct = default)
    {
        var result = await GetRequiredAsync<DashboardVersionResponse>("dashboard/version", ct);
        return (result.Version, result.MatchesExpected);
    }

    public Task<IReadOnlyList<IdentitySession>> ListUserSessionsAsync(string userId, CancellationToken ct = default)
        => GetRequiredAsync<IReadOnlyList<IdentitySession>>("users/" + Id(userId) + "/sessions", ct);

    public Task<AdminResult> LogoutSessionAsync(AdminActor actor, string userId, string sessionId, CancellationToken ct = default)
        => SendResultAsync(HttpMethod.Delete, "users/" + Id(userId) + "/sessions/" + Id(sessionId), null, ct);

    public Task<AdminResult> LogoutAllUserSessionsAsync(AdminActor actor, string userId, CancellationToken ct = default)
        => SendResultAsync(HttpMethod.Delete, "users/" + Id(userId) + "/sessions", null, ct);

    public Task<PagedResult<AdminAuditEntry>> QueryAsync(AdminAuditQuery query, CancellationToken ct = default)
    {
        var parameters = new List<string>
        {
            "page=" + query.Page,
            "pageSize=" + query.PageSize
        };
        AddQuery(parameters, "actorSub", query.ActorSub);
        AddQuery(parameters, "action", query.Action);
        AddQuery(parameters, "targetType", query.TargetType);
        AddQuery(parameters, "result", query.Result);
        AddQuery(parameters, "from", query.From?.ToString("O"));
        AddQuery(parameters, "to", query.To?.ToString("O"));
        return GetRequiredAsync<PagedResult<AdminAuditEntry>>("audit?" + string.Join('&', parameters), ct);
    }

    public Task<AdminResult<RealmExportResult>> ExportAsync(AdminActor actor, RealmExportOptions options, CancellationToken ct = default)
        => SendResultAsync<RealmExportResult>(HttpMethod.Post, "export", options, ct);

    private async Task<T> GetRequiredAsync<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(Root + url, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new AdminApiException(response.StatusCode, "管理 API 返回了空响应。");
    }

    private async Task<T?> GetOrDefaultAsync<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(Root + url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    private async Task<T> PostReadAsync<T>(string url, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(Root + url, body, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new AdminApiException(response.StatusCode, "管理 API 返回了空响应。");
    }

    private Task<AdminResult> SendResultAsync(HttpMethod method, string url, object? body, CancellationToken ct)
        => SendResultCoreAsync<AdminResult>(method, url, body, ct);

    private Task<AdminResult<T>> SendResultAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
        => SendResultCoreAsync<AdminResult<T>>(method, url, body, ct);

    private async Task<TResult> SendResultCoreAsync<TResult>(HttpMethod method, string url, object? body, CancellationToken ct)
        where TResult : class
    {
        using var request = new HttpRequestMessage(method, Root + url);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType());
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == (HttpStatusCode)428)
        {
            await BeginStepUpAsync();
            throw new StepUpRequiredException();
        }

        var result = await TryReadAsync<TResult>(response, ct);
        if (result is not null)
            return result;
        if (!response.IsSuccessStatusCode)
            throw new AdminApiException(response.StatusCode, await ReadErrorAsync(response, ct));
        throw new AdminApiException(response.StatusCode, "管理 API 返回了空响应。");
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == (HttpStatusCode)428)
        {
            await BeginStepUpAsync();
            throw new StepUpRequiredException();
        }
        if (!response.IsSuccessStatusCode)
            throw new AdminApiException(response.StatusCode, await ReadErrorAsync(response, ct));
    }

    private Task BeginStepUpAsync()
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        return session.BeginLoginAsync(string.IsNullOrEmpty(relative) ? "/" : "/" + relative, forceReauthentication: true);
    }

    private static string PageQuery(AdminPageRequest request)
    {
        var query = $"?search={Uri.EscapeDataString(request.Search ?? string.Empty)}&page={request.NormalizedPage}&pageSize={request.NormalizedPageSize}";
        return request.Enabled.HasValue
            ? query + "&enabled=" + request.Enabled.Value.ToString().ToLowerInvariant()
            : query;
    }

    private static void AddQuery(ICollection<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parameters.Add(name + "=" + Uri.EscapeDataString(value));
    }

    private static string Id(string value) => Uri.EscapeDataString(value);

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
        var body = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(body)
            ? $"管理 API 请求失败（HTTP {(int)response.StatusCode}）。"
            : body;
    }

    private sealed record DashboardVersionResponse(string? Version, bool MatchesExpected);
}
