using FileStorageApi.Security;

namespace FileStorageApi.Storage;

/// <summary>
/// 研发阶段：优先读 Claim，其次请求头，最后默认值。
/// ★ P1-7：加 128 字节上限，防止 varchar(128) 溢出触发 500。
///          这是功能性护栏，与认证无关。
/// </summary>
public sealed class HttpCallerContext(IHttpContextAccessor accessor) : ICallerContext
{
    private const int MaxTenantLength = 128;
    private const int MaxActorLength  = 256;

    public string Tenant
    {
        get
        {
            var raw =
                accessor.HttpContext?.User.FindFirst("tenant")?.Value
                ?? accessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                ?? "default";

            if (string.IsNullOrWhiteSpace(raw)) raw = "default";
            if (raw.Length > MaxTenantLength)
                throw new BadHttpRequestException(
                    $"X-Tenant-Id 长度不能超过 {MaxTenantLength}。");
            return raw;
        }
    }

    public string? Actor
    {
        get
        {
            var raw =
                accessor.HttpContext?.User.Identity?.Name
                ?? accessor.HttpContext?.Request.Headers["X-Actor"].FirstOrDefault();

            if (raw is null) return null;
            return raw.Length > MaxActorLength ? raw[..MaxActorLength] : raw;
        }
    }

    public bool IsAuthenticated =>
        accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}