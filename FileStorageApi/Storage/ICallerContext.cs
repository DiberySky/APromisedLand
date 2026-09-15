namespace FileStorageApi.Security;

/// <summary>
/// 调用者上下文：租户、操作者、是否已认证。
/// 业务层只信任此接口，绝不从请求体读取租户。
/// </summary>
public interface ICallerContext
{
    string Tenant { get; }
    string? Actor { get; }
    bool IsAuthenticated { get; }
}
