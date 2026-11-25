namespace TreeGraph.Shared.Identity.Admin;

/// <summary>认证流列表项。</summary>
public sealed record AuthenticationFlowSummary(
    string Id,
    string Alias,
    string? Description,
    string? ProviderId,
    bool BuiltIn,
    bool TopLevel);

/// <summary>认证流执行项。</summary>
public sealed record AuthenticationFlowExecution(
    string Id,
    string? FlowId,
    string? DisplayName,
    string? ProviderId,
    string? Requirement,
    int? Priority,
    bool AuthenticatorFlow,
    string? FlowAlias,
    bool? Configurable);

/// <summary>认证流详情。</summary>
public sealed record AuthenticationFlowDetail(
    AuthenticationFlowSummary Flow,
    IReadOnlyList<AuthenticationFlowExecution> Executions);

/// <summary>认证流绑定（browser/direct grant 等）。</summary>
public sealed record AuthenticationFlowBinding(string BindingType, string? FlowAlias);

/// <summary>复制认证流请求。</summary>
public sealed record CopyAuthenticationFlowRequest(string SourceAlias, string NewAlias);

/// <summary>认证流管理服务。</summary>
public interface IAuthFlowsAdminService
{
    Task<IReadOnlyList<AuthenticationFlowSummary>> ListAsync(CancellationToken ct = default);

    Task<AuthenticationFlowDetail?> GetAsync(string alias, CancellationToken ct = default);

    Task<AdminResult<string>> CopyAsync(AdminActor actor, CopyAuthenticationFlowRequest request, CancellationToken ct = default);

    Task<AdminResult> DeleteAsync(AdminActor actor, string alias, CancellationToken ct = default);

    /// <summary>调整执行项顺序与要求级别。</summary>
    Task<AdminResult> UpdateExecutionAsync(AdminActor actor, string flowAlias, string executionId, string requirement, CancellationToken ct = default);

    Task<IReadOnlyList<AuthenticationFlowBinding>> ListBindingsAsync(CancellationToken ct = default);

    /// <summary>修改绑定属于高危操作，需 step-up 并自动备份。</summary>
    Task<AdminResult> UpdateBindingAsync(AdminActor actor, string bindingType, string flowAlias, CancellationToken ct = default);
}
