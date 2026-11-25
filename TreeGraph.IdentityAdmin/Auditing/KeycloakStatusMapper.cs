using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Auditing;

/// <summary>Keycloak HTTP 状态码到用户可读信息的映射。</summary>
public static class KeycloakStatusMapper
{
    public static string ToMessage(int statusCode) => statusCode switch
    {
        400 => "请求参数不合法（400）",
        401 => "管理令牌无效（401）",
        403 => "Keycloak 拒绝该操作（403）",
        404 => "资源不存在（404）",
        409 => "资源冲突（409）",
        429 => "请求过于频繁（429）",
        >= 500 => $"Keycloak 服务不可用（{statusCode}）",
        _ => $"Keycloak 返回 HTTP {statusCode}"
    };

    public static AdminResult ToResult(int statusCode)
        => AdminResult.Fail(ToMessage(statusCode), statusCode);
}
