namespace TreeGraph.Shared.FileStorageSky.Contracts;

/// <summary>
/// 文件元数据 DTO（客户端契约层）。
/// 由 TreeGraph.FileStorageApi 与前端 Client 共享。
/// </summary>
public sealed record FileMetadataDto
{
    public Guid Id { get; init; }
    public string DocId { get; init; } = string.Empty;
    public int Version { get; init; }
    public string Tenant { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long Size { get; init; }
    public string ObjectKey { get; init; } = string.Empty;
    public string? Sha256 { get; init; }
    public string Status { get; init; } = "active";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
