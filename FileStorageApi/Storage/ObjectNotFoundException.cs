namespace FileStorageApi.Storage;

/// <summary>
/// S3 对象不存在。Controller 捕获后返回 HTTP 404。
/// 与 <see cref="RangeNotSatisfiableException"/>（→ 416）区分。
/// </summary>
public sealed class ObjectNotFoundException : Exception
{
    public string Key { get; }

    public ObjectNotFoundException(string key)
        : base($"对象不存在：{key}")
    {
        Key = key;
    }
}