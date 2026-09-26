using System.Threading.Channels;

namespace FileStorageApi.Files;

public sealed class AuditQueue
{
    private readonly Channel<AuditEntry> _channel =
        Channel.CreateUnbounded<AuditEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    /// <summary>非阻塞入队，永远成功（unbounded channel）。</summary>
    public bool TryEnqueue(AuditEntry entry) => _channel.Writer.TryWrite(entry);

    /// <summary>异步入队（当前 unbounded，不会真正阻塞）。</summary>
    public ValueTask EnqueueAsync(AuditEntry record, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(record, ct);

    /// <summary>
    /// 取一批：首条阻塞等待，避免 CPU 空转；
    /// 拿到首条后尽量填满 max。
    /// </summary>
    public async Task<IReadOnlyList<AuditEntry>> DequeueBatchAsync(
        int max, CancellationToken ct)
    {
        var list = new List<AuditEntry>(max);

        if (await _channel.Reader.WaitToReadAsync(ct))
        {
            while (list.Count < max && _channel.Reader.TryRead(out var item))
            {
                list.Add(item);
            }
        }
        return list;
    }
}