using System.Text.Json.Nodes;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>组织（Keycloak 26 Organizations）管理实现。</summary>
public sealed class OrganizationsAdminService : IOrganizationsAdminService
{
    private const string Module = "organizations";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public OrganizationsAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<PagedResult<OrganizationSummary>> SearchAsync(
        AdminPageRequest request,
        CancellationToken ct = default)
    {
        var query = KeycloakAdminClient.BuildQuery(
            ("search", request.Search),
            ("first", request.First.ToString()),
            ("max", request.Max.ToString()));

        var response = await _client.GetAsync("organizations" + query, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return PagedResult<OrganizationSummary>.Empty(request.NormalizedPage, request.NormalizedPageSize);

        var organizations = response.Deserialize<List<KcOrganization>>() ?? [];
        var items = organizations.Select(o => new OrganizationSummary(
            o.Id ?? string.Empty,
            o.Name,
            o.Alias,
            o.Enabled ?? false,
            o.Description,
            o.Domains?.Count ?? 0)).ToArray();

        var total = organizations.Count < request.Max
            ? request.First + organizations.Count
            : request.First + organizations.Count + 1;

        return new PagedResult<OrganizationSummary>(items, total, request.NormalizedPage, request.NormalizedPageSize);
    }

    public async Task<OrganizationDetail?> GetAsync(string organizationId, CancellationToken ct = default)
    {
        var organization = await _client
            .GetJsonAsync<KcOrganization>($"organizations/{organizationId}", Module, ct)
            .ConfigureAwait(false);

        if (organization is null)
            return null;

        return new OrganizationDetail(
            organization.Id ?? organizationId,
            organization.Name,
            organization.Alias,
            organization.Enabled ?? false,
            organization.Description,
            organization.RedirectUrl,
            organization.Domains ?? [],
            ToFlatAttributes(organization.Attributes));
    }

    public Task<AdminResult<string>> CreateAsync(
        AdminActor actor,
        CreateOrganizationRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            name = request.Name,
            alias = request.Alias,
            enabled = request.Enabled,
            description = request.Description,
            redirectUrl = request.RedirectUrl,
            domains = request.Domains ?? [],
            attributes = ToKcAttributes(request.Attributes)
        };

        return _runner.ExecuteAsync<string>(
            actor,
            "organization.create",
            "organization",
            request.Alias,
            request,
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, "organizations", body, Module, token)
                    .ConfigureAwait(false);
                return (response, response.ExtractCreatedId());
            },
            ct);
    }

    public async Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string organizationId,
        UpdateOrganizationRequest request,
        CancellationToken ct = default)
    {
        var current = await _client.GetAsync($"organizations/{organizationId}", Module, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return KeycloakStatusMapper.ToResult(current.StatusCode);

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return AdminResult.Fail("组织数据解析失败", 502);

        SetIfNotNull(node, "name", request.Name);
        SetIfNotNull(node, "enabled", request.Enabled);
        SetIfNotNull(node, "description", request.Description);
        SetIfNotNull(node, "redirectUrl", request.RedirectUrl);

        if (request.Domains is not null)
        {
            var domains = new JsonArray();
            foreach (var domain in request.Domains)
                domains.Add(domain);

            node["domains"] = domains;
        }

        if (request.Attributes is not null)
        {
            var attributes = new JsonObject();
            foreach (var (key, value) in request.Attributes)
                attributes[key] = new JsonArray(value);

            node["attributes"] = attributes;
        }

        return await _runner.ExecuteAsync(
            actor,
            "organization.update",
            "organization",
            organizationId,
            request,
            token => _client.SendAsync(HttpMethod.Put, $"organizations/{organizationId}", node, Module, token),
            ct).ConfigureAwait(false);
    }

    public Task<AdminResult> DeleteAsync(AdminActor actor, string organizationId, CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "organization.delete",
            "organization",
            organizationId,
            new { organizationId },
            token => _client.SendAsync(HttpMethod.Delete, $"organizations/{organizationId}", null, Module, token),
            ct);

    public async Task<IReadOnlyList<OrganizationMember>> ListMembersAsync(
        string organizationId,
        CancellationToken ct = default)
    {
        var response = await _client
            .GetAsync($"organizations/{organizationId}/members", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return [];

        var users = response.Deserialize<List<KcUser>>() ?? [];
        return users.Select(u => new OrganizationMember(
            u.Id ?? string.Empty,
            u.Username,
            u.Email,
            u.FirstName,
            u.LastName)).ToArray();
    }

    public Task<AdminResult> AddMemberAsync(
        AdminActor actor,
        string organizationId,
        string userId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "organization.add-member",
            "organization",
            organizationId,
            new { organizationId, userId },
            token => _client.SendAsync(
                HttpMethod.Post,
                $"organizations/{organizationId}/members",
                userId,
                Module,
                token),
            ct);

    public Task<AdminResult> RemoveMemberAsync(
        AdminActor actor,
        string organizationId,
        string userId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "organization.remove-member",
            "organization",
            organizationId,
            new { organizationId, userId },
            token => _client.SendAsync(
                HttpMethod.Delete,
                $"organizations/{organizationId}/members/{userId}",
                null,
                Module,
                token),
            ct);

    private static Dictionary<string, string[]> ToKcAttributes(IReadOnlyDictionary<string, string>? attributes)
    {
        var result = new Dictionary<string, string[]>();
        if (attributes is null)
            return result;

        foreach (var (key, value) in attributes)
            result[key] = [value];

        return result;
    }

    private static IReadOnlyDictionary<string, string> ToFlatAttributes(
        Dictionary<string, List<string>>? attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (attributes is null)
            return result;

        foreach (var (key, values) in attributes)
            result[key] = values.Count > 0 ? values[0] : string.Empty;

        return result;
    }

    private static void SetIfNotNull(JsonNode node, string property, string? value)
    {
        if (value is not null)
            node[property] = value;
    }

    private static void SetIfNotNull(JsonNode node, string property, bool? value)
    {
        if (value is not null)
            node[property] = value.Value;
    }
}
