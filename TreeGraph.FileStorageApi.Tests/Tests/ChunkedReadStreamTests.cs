using TreeGraph.FileStorageApi.Storage;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests;

/// <summary>
/// ChunkedReadStream 纯逻辑单测：
/// 把"按索引取块"的委托包装为只读流，验证跨块拼接、EOF、
/// 缺块/空块行为与流属性契约。
/// </summary>
public sealed class ChunkedReadStreamTests
{
    private static ChunkedReadStream Create(IReadOnlyList<byte[]?> chunks)
        => new(
            totalLength: chunks.Where(c => c is not null).Sum(c => c!.Length),
            totalChunks: chunks.Count,
            getChunk: (index, _) => Task.FromResult(chunks[index]));

    // ── 维度 1：跨块拼接 ──

    [Fact]
    public async Task Read_AcrossChunkBoundary_AssemblesInOrder()
    {
        // 块 0 = 3 字节，块 1 = 2 字节；用 1 字节小缓冲逐字节读，强制跨块
        var stream = Create([new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }]);

        var result = new List<byte>();
        var buffer = new byte[1];
        int n;
        while ((n = await stream.ReadAsync(buffer)) > 0)
            result.Add(buffer[0]);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, result.ToArray());
        Assert.Equal(5, stream.Position);
    }

    [Fact]
    public async Task Read_SingleCallLargerThanChunk_ReturnsAllBytes()
    {
        var stream = Create([new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }, new byte[] { 6 }]);

        var buffer = new byte[10];
        var n = await stream.ReadAsync(buffer);

        Assert.Equal(6, n);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, buffer[..n]);
    }

    // ── 维度 2：EOF 与属性 ──

    [Fact]
    public async Task Read_AtEnd_ReturnsZero()
    {
        var stream = Create([new byte[] { 1 }]);

        var buffer = new byte[4];
        await stream.ReadAsync(buffer);
        Assert.Equal(0, await stream.ReadAsync(buffer));
        Assert.Equal(0, await stream.ReadAsync(buffer));
    }

    [Fact]
    public async Task Read_ZeroLengthBuffer_ReturnsZero_WithoutFetchingChunk()
    {
        var calls = 0;
        var stream = new ChunkedReadStream(
            4, 1, (_, _) => { calls++; return Task.FromResult<byte[]?>([1, 2, 3, 4]); });

        Assert.Equal(0, await stream.ReadAsync(Memory<byte>.Empty));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void StreamProperties_ReadOnlyForward()
    {
        var stream = Create([new byte[] { 1 }]);

        Assert.True(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(1, stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    // ── 维度 3：缺块抛异常 / 空块跳过 ──
    //    null 块表示数据缺失（DB 无记录），抛 InvalidDataException 防数据丢失。
    //    空数组（byte[0]）表示存在但零长度，跳过不丢数据。

    [Fact]
    public async Task Read_MissingChunk_ThrowsInvalidData()
    {
        // 块 1 返回 null：修复后抛 InvalidDataException 而非静默跳过
        var stream = Create([new byte[] { 1, 2 }, null, new byte[] { 3 }]);

        var buffer = new byte[10];
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await stream.ReadAsync(buffer));
    }

    [Fact]
    public async Task Read_EmptyChunk_SkipsToNextChunk()
    {
        var stream = Create([new byte[] { 1 }, [], new byte[] { 2 }]);

        var buffer = new byte[10];
        var n = await stream.ReadAsync(buffer);

        Assert.Equal(2, n);
        Assert.Equal(new byte[] { 1, 2 }, buffer[..n]);
    }

    // ── 维度 4：块数耗尽 ──

    [Fact]
    public async Task Read_PastTotalChunks_ReturnsWhatWasAssembled()
    {
        // 2 个块都有数据，读完后 _currentIndex >= _totalChunks 自然 break
        var stream = new ChunkedReadStream(
            totalLength: 6, totalChunks: 2,
            (index, _) => Task.FromResult<byte[]?>([1, 2, 3]));

        var buffer = new byte[10];
        var n = await stream.ReadAsync(buffer);

        Assert.Equal(6, n);
        Assert.Equal(new byte[] { 1, 2, 3, 1, 2, 3 }, buffer[..n]);
    }
}
