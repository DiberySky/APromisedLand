namespace FileStorageApi.Storage;

/// <summary>
/// 请求的 Range 超出对象大小。控制器捕获后返回 HTTP 416。
/// 对应 RFC 7233 §4.4。
///
/// ContentRange 为可选：AWSSDK v4 的异常对象不再暴露响应头，
/// 存储层通常无法得知对象总大小；由控制器用元数据中的 Size 填充。
/// </summary>
public sealed class RangeNotSatisfiableException : Exception
{
    /// <summary>形如 "bytes */12345"。可空；为空时由控制器填充。</summary>
    public string? ContentRange { get; }

    public RangeNotSatisfiableException(string? contentRange = null)
        : base($"Range Not Satisfiable: {contentRange ?? "unknown total"}")
    {
        ContentRange = contentRange;
    }
}