// Storage/ObjectStream.cs
using Amazon.S3.Model;

namespace FileStorageApi.Storage;

/// <summary>
/// 包装 GetObjectResponse，确保 Stream 被 Dispose 时同时释放响应对象。
/// </summary>
internal sealed class ObjectStream : Stream
{
    private readonly GetObjectResponse _response;
    private readonly Stream _inner;

    public ObjectStream(GetObjectResponse response)
    {
        _response = response;
        _inner = response.ResponseStream;
    }

    public override bool CanRead  => _inner.CanRead;
    public override bool CanSeek  => false;
    public override bool CanWrite => false;
    public override long Length   => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
        => _inner.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        => _inner.ReadAsync(buffer, ct);

    public override void Flush() => _inner.Flush();
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
    public override void SetLength(long v) => throw new NotSupportedException();
    public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _response.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        _response.Dispose();
        GC.SuppressFinalize(this);
    }
}