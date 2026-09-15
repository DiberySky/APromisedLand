namespace FileStorageApi.Uploads;

public abstract class UploadException : Exception
{
    protected UploadException(string message) : base(message) { }
}

/// <summary>会话不存在或不属于当前租户 → 404</summary>
public sealed class UploadNotFoundException : UploadException
{
    public UploadNotFoundException(string message) : base(message) { }
}

/// <summary>状态冲突（merging / completed / failed / 并发抢占失败） → 409</summary>
public sealed class UploadConflictException : UploadException
{
    public UploadConflictException(string message) : base(message) { }
}

/// <summary>请求本身不合法（分块不完整 / SHA 不一致 / 参数越界） → 422</summary>
public sealed class UploadValidationException : UploadException
{
    public UploadValidationException(string message) : base(message) { }
}

/// <summary>会话已过期 / 已取消 → 410 Gone</summary>
public sealed class UploadGoneException : UploadException
{
    public UploadGoneException(string message) : base(message) { }
}