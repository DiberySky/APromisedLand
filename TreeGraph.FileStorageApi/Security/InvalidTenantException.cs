namespace TreeGraph.FileStorageApi.Security;

/// <summary>
/// 当 X-Tenant-Id 超过最大长度（128 字节）时抛出。
/// 由全局中间件捕获并映射为 400 Bad Request。
/// </summary>
public sealed class InvalidTenantException : ArgumentException
{
    public InvalidTenantException(string message) : base(message) { }
}
