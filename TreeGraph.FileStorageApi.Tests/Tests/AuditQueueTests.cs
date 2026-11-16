using TreeGraph.FileStorageApi.Files;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests;

/// <summary>
/// AuditQueue 纯逻辑单测：无界 Channel 的入队/批量出队/FIFO 语义。
/// </summary>
public sealed class AuditQueueTests
{
    private static AuditEntry Entry(string docId) => new(
        DocId: docId, Tenant: "t", Action: "downloaded", Actor: null, DetailsJson: "{}");

    // ── 维度 1：入队成功 ──

    [Fact]
    public void TryEnqueue_AlwaysSucceeds_Unbounded()
    {
        var queue = new AuditQueue();

        for (var i = 0; i < 1000; i++)
            Assert.True(queue.TryEnqueue(Entry($"doc-{i}")));
    }

    // ── 维度 2：批量出队与 FIFO ──

    [Fact]
    public async Task DequeueBatch_ReturnsItemsInFifoOrder()
    {
        var queue = new AuditQueue();
        for (var i = 0; i < 5; i++) queue.TryEnqueue(Entry($"doc-{i}"));

        var batch = await queue.DequeueBatchAsync(10, CancellationToken.None);

        Assert.Equal(5, batch.Count);
        Assert.Equal(Enumerable.Range(0, 5).Select(i => $"doc-{i}"), batch.Select(e => e.DocId));
    }

    [Fact]
    public async Task DequeueBatch_RespectsMax_AndReturnsRemainderNext()
    {
        var queue = new AuditQueue();
        for (var i = 0; i < 5; i++) queue.TryEnqueue(Entry($"doc-{i}"));

        var first = await queue.DequeueBatchAsync(2, CancellationToken.None);
        var second = await queue.DequeueBatchAsync(2, CancellationToken.None);
        var third = await queue.DequeueBatchAsync(2, CancellationToken.None);

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Single(third);
        Assert.Equal(["doc-4"], third.Select(e => e.DocId));
    }

    [Fact]
    public async Task DequeueBatch_PreservesFifo_WhenConcurrentWritesArrive()
    {
        // 首条阻塞等待；写入与出队并发时，一批可能只拿到首条，最终仍须完整保持 FIFO。
        var queue = new AuditQueue();

        var dequeueTask = queue.DequeueBatchAsync(3, CancellationToken.None);
        queue.TryEnqueue(Entry("a"));
        queue.TryEnqueue(Entry("b"));
        queue.TryEnqueue(Entry("c"));

        var batch = await dequeueTask;
        var remaining = new List<AuditEntry>();
        while (batch.Count + remaining.Count < 3)
            remaining.AddRange(await queue.DequeueBatchAsync(3 - batch.Count - remaining.Count, CancellationToken.None));

        Assert.Equal(["a", "b", "c"], batch.Concat(remaining).Select(e => e.DocId));
    }

    // ── 维度 3：取消语义 ──

    [Fact]
    public async Task DequeueBatch_EmptyChannel_WithCanceledToken_ThrowsOperationCanceled()
    {
        var queue = new AuditQueue();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // WaitToReadAsync 取消时抛 TaskCanceledException（OCE 子类）
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => queue.DequeueBatchAsync(1, cts.Token));
    }
}
