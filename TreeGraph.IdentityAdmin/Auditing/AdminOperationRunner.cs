using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreeGraph.IdentityAdmin.Audit;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Auditing;

/// <summary>
/// 管理写操作的统一执行器：两阶段审计 + fail-closed + 兜底告警。
/// Keycloak 调用前后各写一次审计；审计库不可用时拒绝执行。
/// </summary>
public sealed class AdminOperationRunner
{
    private static readonly Meter Meter = new("TreeGraph.IdentityAdmin", "1.0.0");
    private static readonly Counter<long> AuditWriteFailures =
        Meter.CreateCounter<long>("admin_audit_write_failures_total");

    private readonly IAdminAuditStore _auditStore;
    private readonly IOptionsMonitor<KeycloakAdminOptions> _options;
    private readonly ILogger<AdminOperationRunner> _logger;

    public AdminOperationRunner(
        IAdminAuditStore auditStore,
        IOptionsMonitor<KeycloakAdminOptions> options,
        ILogger<AdminOperationRunner> logger)
    {
        _auditStore = auditStore;
        _options = options;
        _logger = logger;
    }

    public async Task<AdminResult> ExecuteAsync(
        AdminActor actor,
        string action,
        string? targetType,
        string? targetId,
        object? request,
        Func<CancellationToken, Task<KeycloakResponse>> operation,
        CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var auditId = await TryBeginAsync(actor, action, targetType, targetId, request, correlationId, ct)
            .ConfigureAwait(false);

        if (auditId is null)
            return AdminResult.Fail("审计库不可用，操作已拒绝", 503);

        try
        {
            var response = await operation(ct).ConfigureAwait(false);
            var result = response.IsSuccess ? "success" : "failed";
            var error = response.IsSuccess ? null : KeycloakStatusMapper.ToMessage(response.StatusCode);

            await CompleteSafeAsync(
                    auditId.Value,
                    result,
                    response.StatusCode,
                    error,
                    BuildFallbackLine(correlationId, action, result, response.StatusCode, error),
                    ct)
                .ConfigureAwait(false);

            return response.IsSuccess
                ? AdminResult.Ok()
                : AdminResult.Fail(error ?? "操作失败", response.StatusCode);
        }
        catch (KeycloakUnauthorizedException ex)
        {
            await CompleteSafeAsync(
                    auditId.Value,
                    "failed",
                    401,
                    ex.Message,
                    BuildFallbackLine(correlationId, action, "failed", 401, ex.Message),
                    ct)
                .ConfigureAwait(false);

            return AdminResult.Fail("管理令牌无效或权限不足（401）", 401);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "管理操作异常：{Action} correlationId={CorrelationId}", action, correlationId);
            await CompleteSafeAsync(
                    auditId.Value,
                    "failed",
                    null,
                    ex.Message,
                    BuildFallbackLine(correlationId, action, "failed", null, ex.Message),
                    ct)
                .ConfigureAwait(false);

            return AdminResult.Fail("调用 Keycloak 失败：" + ex.Message, 502);
        }
    }

    public async Task<AdminResult<T>> ExecuteAsync<T>(
        AdminActor actor,
        string action,
        string? targetType,
        string? targetId,
        object? request,
        Func<CancellationToken, Task<(KeycloakResponse Response, T? Value)>> operation,
        CancellationToken ct = default,
        Func<T, string>? successResultLabel = null)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var auditId = await TryBeginAsync(actor, action, targetType, targetId, request, correlationId, ct)
            .ConfigureAwait(false);

        if (auditId is null)
            return AdminResult<T>.Fail("审计库不可用，操作已拒绝", 503);

        try
        {
            var (response, value) = await operation(ct).ConfigureAwait(false);
            var result = response.IsSuccess
                ? successResultLabel is not null && value is not null
                    ? successResultLabel(value)
                    : "success"
                : "failed";
            var error = response.IsSuccess ? null : KeycloakStatusMapper.ToMessage(response.StatusCode);

            await CompleteSafeAsync(
                    auditId.Value,
                    result,
                    response.StatusCode,
                    error,
                    BuildFallbackLine(correlationId, action, result, response.StatusCode, error),
                    ct)
                .ConfigureAwait(false);

            return response.IsSuccess
                ? AdminResult<T>.Ok(value!)
                : AdminResult<T>.Fail(error ?? "操作失败", response.StatusCode);
        }
        catch (KeycloakUnauthorizedException ex)
        {
            await CompleteSafeAsync(
                    auditId.Value,
                    "failed",
                    401,
                    ex.Message,
                    BuildFallbackLine(correlationId, action, "failed", 401, ex.Message),
                    ct)
                .ConfigureAwait(false);

            return AdminResult<T>.Fail("管理令牌无效或权限不足（401）", 401);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "管理操作异常：{Action} correlationId={CorrelationId}", action, correlationId);
            await CompleteSafeAsync(
                    auditId.Value,
                    "failed",
                    null,
                    ex.Message,
                    BuildFallbackLine(correlationId, action, "failed", null, ex.Message),
                    ct)
                .ConfigureAwait(false);

            return AdminResult<T>.Fail("调用 Keycloak 失败：" + ex.Message, 502);
        }
    }

    private async Task<Guid?> TryBeginAsync(
        AdminActor actor,
        string action,
        string? targetType,
        string? targetId,
        object? request,
        string correlationId,
        CancellationToken ct)
    {
        try
        {
            return await _auditStore.BeginAsync(
                    actor.Sub,
                    actor.Username,
                    _options.CurrentValue.AdminClientId,
                    action,
                    targetType,
                    targetId,
                    AdminRequestSanitizer.Serialize(request),
                    correlationId,
                    ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "审计库不可用，拒绝执行管理操作：{Action}", action);
            AuditWriteFailures.Add(1);
            return null;
        }
    }

    private async Task CompleteSafeAsync(
        Guid auditId,
        string result,
        int? statusCode,
        string? error,
        string fallbackLine,
        CancellationToken ct)
    {
        try
        {
            await _auditStore.CompleteAsync(auditId, result, statusCode, error, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审计结果写入失败，转本地兜底：{Line}", fallbackLine);
            AuditWriteFailures.Add(1);
            await _auditStore.WriteFallbackAsync(fallbackLine, ct).ConfigureAwait(false);
        }
    }

    private static string BuildFallbackLine(
        string correlationId,
        string action,
        string result,
        int? statusCode,
        string? error)
        => string.Concat(
            "correlationId=", correlationId,
            " action=", action,
            " result=", result,
            " status=", statusCode?.ToString() ?? "-",
            " error=", error ?? "-");
}
