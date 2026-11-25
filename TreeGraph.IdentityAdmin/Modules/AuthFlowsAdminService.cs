using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Backup;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>认证流管理实现。</summary>
public sealed class AuthFlowsAdminService : IAuthFlowsAdminService
{
    private const string Module = "auth-flows";

    private static readonly string[] BindingProperties =
    [
        "browserFlow",
        "registrationFlow",
        "directGrantFlow",
        "resetCredentialsFlow",
        "clientAuthenticationFlow",
        "dockerAuthenticationFlow",
        "firstBrokerLoginFlow"
    ];

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;
    private readonly IExportAdminService _exportService;
    private readonly IRealmBackupStore _backupStore;
    private readonly ILogger<AuthFlowsAdminService> _logger;

    public AuthFlowsAdminService(
        KeycloakAdminClient client,
        AdminOperationRunner runner,
        IExportAdminService exportService,
        IRealmBackupStore backupStore,
        ILogger<AuthFlowsAdminService> logger)
    {
        _client = client;
        _runner = runner;
        _exportService = exportService;
        _backupStore = backupStore;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AuthenticationFlowSummary>> ListAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync("authentication/flows", Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return [];

        var flows = response.Deserialize<List<KcAuthenticationFlow>>() ?? [];
        return flows.Select(Map).ToArray();
    }

    public async Task<AuthenticationFlowDetail?> GetAsync(string alias, CancellationToken ct = default)
    {
        var flows = await ListAsync(ct).ConfigureAwait(false);
        var flow = flows.FirstOrDefault(f => string.Equals(f.Alias, alias, StringComparison.Ordinal));
        if (flow is null)
            return null;

        var executions = await LoadExecutionsAsync(alias, ct).ConfigureAwait(false);
        return new AuthenticationFlowDetail(flow, executions);
    }

    public async Task<AdminResult<string>> CopyAsync(
        AdminActor actor,
        CopyAuthenticationFlowRequest request,
        CancellationToken ct = default)
    {
        var backup = await BackupBeforeWriteAsync(actor, "auth-flow.copy", ct).ConfigureAwait(false);
        if (backup is not null)
            return new AdminResult<string>(false, null, backup.Error, backup.StatusCode);

        var body = new { newName = request.NewAlias };

        return await _runner.ExecuteAsync<string>(
            actor,
            "auth-flow.copy",
            "auth-flow",
            request.SourceAlias,
            request,
            async token =>
            {
                var response = await _client
                    .SendAsync(
                        HttpMethod.Post,
                        $"authentication/flows/{Uri.EscapeDataString(request.SourceAlias)}/copy",
                        body,
                        Module,
                        token)
                    .ConfigureAwait(false);
                return (response, request.NewAlias);
            },
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> DeleteAsync(AdminActor actor, string alias, CancellationToken ct = default)
    {
        var backup = await BackupBeforeWriteAsync(actor, "auth-flow.delete", ct).ConfigureAwait(false);
        if (backup is not null) return backup;

        return await _runner.ExecuteAsync(
            actor,
            "auth-flow.delete",
            "auth-flow",
            alias,
            new { alias },
            token => _client.SendAsync(
                HttpMethod.Delete,
                $"authentication/flows/{Uri.EscapeDataString(alias)}",
                null,
                Module,
                token),
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> UpdateExecutionAsync(
        AdminActor actor,
        string flowAlias,
        string executionId,
        string requirement,
        CancellationToken ct = default)
    {
        var backup = await BackupBeforeWriteAsync(actor, "auth-flow.update-execution", ct).ConfigureAwait(false);
        if (backup is not null) return backup;

        var response = await _client
            .GetAsync($"authentication/flows/{Uri.EscapeDataString(flowAlias)}/executions", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return KeycloakStatusMapper.ToResult(response.StatusCode);

        var executions = JsonNode.Parse(response.Body)?.AsArray();
        if (executions is null)
            return AdminResult.Fail("认证执行项数据解析失败", 502);

        JsonObject? target = null;
        foreach (var item in executions)
        {
            if (item is JsonObject obj
                && string.Equals((string?)obj["id"], executionId, StringComparison.Ordinal))
            {
                target = obj;
                break;
            }
        }

        if (target is null)
            return AdminResult.Fail("认证执行项不存在（404）", 404);

        target["requirement"] = requirement;

        return await _runner.ExecuteAsync(
            actor,
            "auth-flow.update-execution",
            "auth-flow",
            flowAlias,
            new { flowAlias, executionId, requirement },
            token => _client.SendAsync(
                HttpMethod.Put,
                $"authentication/flows/{Uri.EscapeDataString(flowAlias)}/executions",
                target,
                Module,
                token),
            ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuthenticationFlowBinding>> ListBindingsAsync(CancellationToken ct = default)
    {
        var realm = await _client.GetAsync(string.Empty, Module, ct).ConfigureAwait(false);
        if (!realm.IsSuccess)
            return [];

        var node = JsonNode.Parse(realm.Body)?.AsObject();
        if (node is null)
            return [];

        var bindings = new List<AuthenticationFlowBinding>();
        foreach (var property in BindingProperties)
            bindings.Add(new AuthenticationFlowBinding(property, (string?)node[property]));

        return bindings;
    }

    public async Task<AdminResult> UpdateBindingAsync(
        AdminActor actor,
        string bindingType,
        string flowAlias,
        CancellationToken ct = default)
    {
        if (!BindingProperties.Contains(bindingType, StringComparer.Ordinal))
            return AdminResult.Fail("不支持的绑定类型（400）", 400);

        // 验收：每次认证流写操作前自动导出备份；导出失败则拒绝写入（fail-closed）。
        var backup = await BackupBeforeWriteAsync(actor, "auth-flow.update-binding", ct).ConfigureAwait(false);
        if (backup is not null) return backup;

        var realm = await _client.GetAsync(string.Empty, Module, ct).ConfigureAwait(false);
        if (!realm.IsSuccess)
            return KeycloakStatusMapper.ToResult(realm.StatusCode);

        var node = JsonNode.Parse(realm.Body)?.AsObject();
        if (node is null)
            return AdminResult.Fail("realm 数据解析失败", 502);

        node[bindingType] = flowAlias;

        return await _runner.ExecuteAsync(
            actor,
            "auth-flow.update-binding",
            "auth-flow",
            bindingType,
            new { bindingType, flowAlias },
            token => _client.SendAsync(HttpMethod.Put, string.Empty, node, Module, token),
            ct).ConfigureAwait(false);
    }

    private async Task<AdminResult?> BackupBeforeWriteAsync(AdminActor actor, string action, CancellationToken ct)
    {
        try
        {
            var backup = await _exportService.ExportAsync(actor, new RealmExportOptions(), ct).ConfigureAwait(false);
            if (!backup.Success || backup.Value is null)
            {
                _logger.LogWarning("认证流写操作（{Action}）前备份失败，拒绝写入：{Error}", action, backup.Error);
                return AdminResult.Fail("认证流写操作前自动备份失败，已拒绝写入", 503);
            }

            // 加密持久化：存库失败同样拒绝写入（fail-closed）。
            await _backupStore.SaveAsync(
                    _client.Realm,
                    backup.Value.SuggestedFileName,
                    backup.Value.Json,
                    actor.Sub,
                    action,
                    ct)
                .ConfigureAwait(false);

            _logger.LogInformation("认证流写操作（{Action}）前已自动备份：{File}", action, backup.Value.SuggestedFileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "认证流写操作（{Action}）前备份异常，拒绝写入", action);
            return AdminResult.Fail("认证流写操作前自动备份异常，已拒绝写入", 503);
        }
    }

    private async Task<IReadOnlyList<AuthenticationFlowExecution>> LoadExecutionsAsync(
        string alias,
        CancellationToken ct)
    {
        var response = await _client
            .GetAsync($"authentication/flows/{Uri.EscapeDataString(alias)}/executions", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return [];

        var executions = response.Deserialize<List<KcAuthenticationExecutionInfo>>() ?? [];
        return executions.Select(e => new AuthenticationFlowExecution(
            e.Id ?? string.Empty,
            e.FlowId,
            e.DisplayName,
            e.ProviderId,
            e.Requirement,
            e.Priority,
            e.AuthenticationFlow ?? false,
            e.FlowAlias,
            e.Configurable)).ToArray();
    }

    private static AuthenticationFlowSummary Map(KcAuthenticationFlow flow)
        => new(
            flow.Id ?? string.Empty,
            flow.Alias ?? string.Empty,
            flow.Description,
            flow.ProviderId,
            flow.BuiltIn ?? false,
            flow.TopLevel ?? false);
}
