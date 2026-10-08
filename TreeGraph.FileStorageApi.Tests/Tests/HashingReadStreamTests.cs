using System.Security.Cryptography;
using TreeGraph.FileStorageApi.Storage;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests;

/// <summary>
/// HashingReadStream 纯逻辑单测：读取时累积 SHA256、EOF 锁定哈希、
/// 同步/异步两种读取路径、未读完时 Hash 为 null。
/// </summary>
public sealed class HashingReadStreamTests
{
    private static string ExpectedSha256(byte[] payload)
        => Convert.ToHexString(SHA256.HashData(payload));

    private static byte[] Payload(int size)
        => Enumerable.Range(0, size).Select(i => (byte)(i % 251)).ToArray();

    // ── 维度 1：哈希正确性 ──

    [Fact]
    public async Task ReadAsync_ToEof_LocksHashMatchingSha256()
    {
        var payload = Payload(100_000);   // > 内部缓冲，强制多次读取
        using var hashing = new HashingReadStream(new MemoryStream(payload));

        var drain = new byte[8192];
        while (await hashing.ReadAsync(drain) > 0) { }

        Assert.Equal(ExpectedSha256(payload), hashing.Hash);
    }

    [Fact]
    public void ReadSync_ToEof_LocksHashMatchingSha256()
    {
        var payload = Payload(50_000);
        using var hashing = new HashingReadStream(new MemoryStream(payload));

        var drain = new byte[4096];
        while (hashing.Read(drain, 0, drain.Length) > 0) { }

        Assert.Equal(ExpectedSha256(payload), hashing.Hash);
    }

    [Fact]
    public async Task SmallReads_ProduceSameHashAsSingleLargeRead()
    {
        var payload = Payload(10_000);

        string viaSmall;
        using (var hashing = new HashingReadStream(new MemoryStream(payload)))
        {
            var one = new byte[1];
            while (await hashing.ReadAsync(one) > 0) { }
            viaSmall = hashing.Hash!;
        }

        Assert.Equal(ExpectedSha256(payload), viaSmall);
    }

    // ── 维度 2：EOF 语义 ──

    [Fact]
    public void Hash_IsNull_BeforeEof()
    {
        var payload = Payload(100);
        using var hashing = new HashingReadStream(new MemoryStream(payload));

        var drain = new byte[64];
        hashing.Read(drain, 0, drain.Length);   // 只读一半

        Assert.Null(hashing.Hash);
    }

    [Fact]
    public async Task Hash_StaysLocked_AfterFurtherReadsReturnZero()
    {
        var payload = Payload(10);
        using var hashing = new HashingReadStream(new MemoryStream(payload));

        var drain = new byte[64];
        while (await hashing.ReadAsync(drain) > 0) { }   // 读到 EOF 锁定
        var first = hashing.Hash;

        Assert.NotNull(first);
        await hashing.ReadAsync(drain);                  // 再读不改变哈希
        Assert.Same(first, hashing.Hash);
    }

    [Fact]
    public async Task ReadAsync_AtEof_ReturnsZero_AndHashNotNull()
    {
        var payload = Payload(10);
        using var hashing = new HashingReadStream(new MemoryStream(payload));

        var drain = new byte[64];
        await hashing.ReadAsync(drain);
        Assert.Equal(0, await hashing.ReadAsync(drain));
        Assert.NotNull(hashing.Hash);
    }

    // ── 维度 3：流属性与释放 ──

    [Fact]
    public void StreamProperties_ForwardReadOnly()
    {
        var payload = Payload(16);
        using var hashing = new HashingReadStream(new MemoryStream(payload));

        Assert.True(hashing.CanRead);
        Assert.False(hashing.CanSeek);
        Assert.False(hashing.CanWrite);
        Assert.Equal(16, hashing.Length);
        Assert.Throws<NotSupportedException>(() => hashing.Position = 0);
        Assert.Throws<NotSupportedException>(() => hashing.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => hashing.Write([1], 0, 1));
        Assert.Throws<NotSupportedException>(() => hashing.SetLength(0));
    }

    [Fact]
    public async Task DisposeAsync_DisposesInnerStream()
    {
        var inner = new MemoryStream(Payload(8));
        var hashing = new HashingReadStream(inner);

        await hashing.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => inner.ReadByte());
    }
}
