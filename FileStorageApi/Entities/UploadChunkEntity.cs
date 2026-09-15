// Entities/UploadChunkEntity.cs
namespace FileStorageApi.Entities;

public class UploadChunkEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UploadId { get; set; }
    public int ChunkIndex { get; set; }
    public int Size { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string? Sha256 { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public UploadSessionEntity? Session { get; set; }
}