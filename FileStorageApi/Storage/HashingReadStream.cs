using System.Security.Cryptography;

namespace FileStorageApi.Storage;

/// <summary>
/// 装饰流：读取时同步累积 SHA256，读到 EOF 时锁定十六进制哈希。
/// 用于把"分块合并 → SHA 计算 → S3 上传"压缩为一次 DB 扫描。
/// </summary>
public sealed class HashingReadStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hasher;
    private string? _hash;

    public HashingReadStream(Stream inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    }

    /// <summary>完整读取到 EOF 后的 SHA256（大写十六进制）。未读完为 null。</summary>
    public string? Hash => _hash;

    public override bool CanRead  => _inner.CanRead;
    public override bool CanSeek  => false;
    public override bool CanWrite => false;
    public override long Length   => _inner.Length;
    public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken ct = default)
    {
        var n = await _inner.ReadAsync(buffer, ct).ConfigureAwait(false);
        if (n > 0)
        {
            _hasher.AppendData(buffer.Span.Slice(0, n));
        }
        else
        {
            _hash ??= Convert.ToHexString(_hasher.GetHashAndReset());
        }
        return n;
    }

    public override void Flush() { }
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
    public override void SetLength(long v) => throw new NotSupportedException();
    public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hasher.Dispose();
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        _hasher.Dispose();
        await _inner.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}