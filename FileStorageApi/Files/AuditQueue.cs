using System.Threading.Channels;

namespace FileStorageApi.Files;

/// <summary>审计写入条目（与 DbContext 解耦，由后台服务批量落库）。</summary>
public sealed record AuditEntry(
    string DocId,
    string Tenant,
    string Action,
    string? Actor,
    string? DetailsJson);

/// <summary>
/// 审计写入队列。有界 Channel + DropOldest：
///   - 满时丢弃最旧，避免阻塞请求线程
///   - 单读者（AuditWriterService），多写者（请求线程）
///   - 应用关闭时残留条目可能丢失（研发阶段可接受）
/// </summary>
public sealed class AuditQueue
{
    private const int DefaultCapacity = 10_000;

    private readonly Channel<AuditEntry> _channel;

    public AuditQueue(int capacity = DefaultCapacity)
    {
        _channel = Channel.CreateBounded<AuditEntry>(
            new BoundedChannelOptions(capacity)
            {
                FullMode     = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
    }

    /// <summary>非阻塞入队。队列满时返回 false（旧条目被丢弃）。</summary>
    public bool TryEnqueue(AuditEntry entry) => _channel.Writer.TryWrite(entry);

    public ChannelReader<AuditEntry> Reader => _channel.Reader;
}