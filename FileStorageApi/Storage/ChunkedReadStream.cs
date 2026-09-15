namespace FileStorageApi.Storage;

/// <summary>
/// 把"按索引逐块获取字节数组"的异步委托包装成只读 Stream。
/// 内存中同一时刻只持有一个分块（默认 4 MB），避免整文件加载。
/// </summary>
public sealed class ChunkedReadStream : Stream
{
    private readonly Func<int, CancellationToken, Task<byte[]?>> _getChunk;
    private readonly int _totalChunks;
    private readonly long _totalLength;

    private int _currentIndex = -1;
    private byte[]? _currentBuffer;
    private int _currentOffset;
    private long _position;

    public ChunkedReadStream(
        long totalLength,
        int totalChunks,
        Func<int, CancellationToken, Task<byte[]?>> getChunk)
    {
        _totalLength = totalLength;
        _totalChunks = totalChunks;
        _getChunk = getChunk;
    }

    public override bool CanRead  => true;
    public override bool CanSeek  => false;
    public override bool CanWrite => false;
    public override long Length   => _totalLength;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken ct = default)
    {
        if (buffer.Length == 0) return 0;
        if (_position >= _totalLength) return 0;

        var written = 0;
        while (written < buffer.Length)
        {
            // 当前块已耗尽 → 预取下一块
            if (_currentBuffer is null || _currentOffset >= _currentBuffer.Length)
            {
                _currentIndex++;
                if (_currentIndex >= _totalChunks) break;

                _currentBuffer = await _getChunk(_currentIndex, ct).ConfigureAwait(false);
                _currentOffset = 0;

                if (_currentBuffer is null || _currentBuffer.Length == 0) continue;
            }

            var available = _currentBuffer.Length - _currentOffset;
            var need = buffer.Length - written;
            var take = Math.Min(available, need);

            _currentBuffer.AsSpan(_currentOffset, take)
                          .CopyTo(buffer.Span.Slice(written, take));

            _currentOffset += take;
            written += take;
            _position += take;

            if (written == buffer.Length) break;
        }

        return written;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}