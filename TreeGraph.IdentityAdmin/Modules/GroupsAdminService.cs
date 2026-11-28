using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>组与空间组成员管理实现。</summary>
public sealed class GroupsAdminService : IGroupsAdminService
{
    private const string Module = "groups";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public GroupsAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<IReadOnlyList<IdentityGroup>> GetTreeAsync(bool brief = true, CancellationToken ct = default)
    {
        var response = await _client
            .GetAsync($"groups?briefRepresentation={brief.ToString().ToLowerInvariant()}&first=0&max=200", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return [];

        var groups = response.Deserialize<List<KcGroup>>() ?? [];
        var result = new List<IdentityGroup>(groups.Count);
        foreach (var group in groups)
            result.Add(await MapAsync(group, brief, ct).ConfigureAwait(false));

        return result;
    }

    public async Task<IdentityGroup?> GetAsync(string groupId, CancellationToken ct = default)
    {
        var group = await _client.GetJsonAsync<KcGroup>($"groups/{groupId}", Module, ct).ConfigureAwait(false);
        return group is null ? null : await MapAsync(group, false, ct).ConfigureAwait(false);
    }

    public Task<AdminResult<string>> CreateAsync(
        AdminActor actor,
        CreateGroupRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            name = request.Name,
            parentId = request.ParentId,
            attributes = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : new Dictionary<string, string[]> { ["description"] = [request.Description] }
        };

        return _runner.ExecuteAsync<string>(
            actor,
            "group.create",
            "group",
            request.Name,
            request,
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, "groups", body, Module, token)
                    .ConfigureAwait(false);
                return (response, response.ExtractCreatedId());
            },
            ct);
    }

    public async Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string groupId,
        UpdateGroupRequest request,
        CancellationToken ct = default)
    {
        var current = await _client.GetJsonAsync<KcGroup>($"groups/{groupId}", Module, ct).ConfigureAwait(false);
        if (current is null)
            return AdminResult.Fail("组不存在（404）", 404);

        var attributes = current.Attributes ?? new Dictionary<string, List<string>>();
        if (request.Description is not null)
            attributes["description"] = [request.Description];

        var body = new
        {
            id = current.Id ?? groupId,
            name = request.Name ?? current.Name,
            path = current.Path,
            attributes = attributes.ToDictionary(p => p.Key, p => p.Value.ToArray())
        };

        return await _runner.ExecuteAsync(
            actor,
            "group.update",
            "group",
            groupId,
            request,
            token => _client.SendAsync(HttpMethod.Put, $"groups/{groupId}", body, Module, token),
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> DeleteAsync(AdminActor actor, string groupId, CancellationToken ct = default)
    {
        var current = await _client.GetJsonAsync<KcGroup>($"groups/{groupId}", Module, ct).ConfigureAwait(false);
        if (current is null)
            return AdminResult.Fail("组不存在（404）", 404);

        var path = current.Path ?? string.Empty;
        if (path.StartsWith(IdentityConstants.SpacesGroupPath + "/", StringComparison.Ordinal))
            return AdminResult.Fail("空间组不接受从通用组管理页删除，请通过业务侧解绑", 409);

        return await _runner.ExecuteAsync(
            actor,
            "group.delete",
            "group",
            groupId,
            new { groupId },
            token => _client.SendAsync(HttpMethod.Delete, $"groups/{groupId}", null, Module, token),
            ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IdentityUserSummary>> ListMembersAsync(string groupId, CancellationToken ct = default)
    {
        var response = await _client
            .GetAsync($"groups/{groupId}/members?first=0&max=200", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return [];

        var users = response.Deserialize<List<KcUser>>() ?? [];
        return users.Select(u => new IdentityUserSummary(
            u.Id ?? string.Empty,
            u.Username ?? string.Empty,
            u.Attributes is not null && u.Attributes.TryGetValue("realName", out var realName) && realName.Count > 0
                ? realName[0]
                : null,
            u.Attributes is not null && u.Attributes.TryGetValue("phoneNumber", out var phone) && phone.Count > 0
                ? phone[0]
                : null,
            u.Enabled ?? false,
            u.CreatedTimestamp)).ToArray();
    }

    public Task<AdminResult> AddMemberAsync(
        AdminActor actor,
        string groupId,
        string userId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "group.add-member",
            "group",
            groupId,
            new { groupId, userId },
            token => _client.SendAsync(HttpMethod.Put, $"groups/{groupId}/members/{userId}", null, Module, token),
            ct);

    public Task<AdminResult> RemoveMemberAsync(
        AdminActor actor,
        string groupId,
        string userId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "group.remove-member",
            "group",
            groupId,
            new { groupId, userId },
            token => _client.SendAsync(HttpMethod.Delete, $"groups/{groupId}/members/{userId}", null, Module, token),
            ct);

    public async Task<AdminResult<string>> EnsureSpaceGroupAsync(
        AdminActor actor,
        string spaceId,
        CancellationToken ct = default)
    {
        var spaces = await FindTopLevelGroupAsync("spaces", ct).ConfigureAwait(false);
        string spacesId;

        if (spaces is null)
        {
            var createSpaces = await CreateAsync(actor, new CreateGroupRequest("spaces"), ct).ConfigureAwait(false);
            if (!createSpaces.Success || string.IsNullOrEmpty(createSpaces.Value))
                return AdminResult<string>.Fail(createSpaces.Error ?? "创建 /spaces 组失败", createSpaces.StatusCode);

            spacesId = createSpaces.Value;
        }
        else
        {
            spacesId = spaces.Id;
        }

        var groupName = spaceId.ToSpaceGroupName();
        var children = await LoadChildrenAsync(spacesId, ct).ConfigureAwait(false);
        var existing = children.FirstOrDefault(c => string.Equals(c.Name, groupName, StringComparison.Ordinal));
        if (existing is not null && !string.IsNullOrEmpty(existing.Id))
            return AdminResult<string>.Ok(existing.Id);

        return await CreateAsync(
            actor,
            new CreateGroupRequest(groupName, spacesId, $"空间 {spaceId}"),
            ct).ConfigureAwait(false);
    }

    private async Task<IdentityGroup> MapAsync(KcGroup group, bool brief, CancellationToken ct)
    {
        var children = new List<IdentityGroup>();
        if (!brief)
        {
            var subGroups = group.SubGroups ?? [];
            if (subGroups.Count == 0)
                subGroups = await LoadChildrenAsync(group.Id ?? string.Empty, ct).ConfigureAwait(false);

            foreach (var child in subGroups)
                children.Add(await MapAsync(child, false, ct).ConfigureAwait(false));
        }

        return new IdentityGroup(
            group.Id ?? string.Empty,
            group.Name ?? string.Empty,
            group.Path ?? string.Empty,
            ReadDescription(group),
            null,
            children);
    }

    private async Task<List<KcGroup>> LoadChildrenAsync(string parentId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(parentId))
            return [];

        var response = await _client
            .GetAsync($"groups/{parentId}/children?briefRepresentation=false&first=0&max=200", Module, ct)
            .ConfigureAwait(false);

        return response.IsSuccess
            ? response.Deserialize<List<KcGroup>>() ?? []
            : [];
    }

    private async Task<KcGroup?> FindTopLevelGroupAsync(string name, CancellationToken ct)
    {
        var query = KeycloakAdminClient.BuildQuery(
            ("search", name),
            ("briefRepresentation", "false"),
            ("first", "0"),
            ("max", "200"));

        var response = await _client.GetAsync("groups" + query, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return null;

        var groups = response.Deserialize<List<KcGroup>>() ?? [];
        return groups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.Ordinal));
    }

    private static string? ReadDescription(KcGroup group)
        => group.Attributes is not null
           && group.Attributes.TryGetValue("description", out var values)
           && values.Count > 0
            ? values[0]
            : null;
}
