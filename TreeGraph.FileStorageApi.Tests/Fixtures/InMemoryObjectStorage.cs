using System.Collections.Concurrent;
using TreeGraph.FileStorageApi.Storage;

namespace TreeGraph.FileStorageApi.Tests.Fixtures;

/// <summary>
/// IObjectStorage 的内存实现，模拟 S3 语义：
///   - GetAsync 支持 [start, end] 闭区间 Range，越界抛 RangeNotSatisfiableException
///   - GetAsync 返回 ContentLength / TotalLength / ContentRange（"bytes s-e/total"）
///   - DeleteAsync 对不存在的 key 返回 false
/// 提供故障注入点（DeleteOverride / PutException）供清理与失败路径测试使用。
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    /// <summary>DeleteAsync 调用计数（含失败的），供清理测试断言。</summary>
    public int DeleteCallCount;

    /// <summary>返回 true 表示删除成功，false 表示 S3 报告删除失败。默认 null = 正常删除。</summary>
    public Func<string, Task<bool>>? DeleteOverride;

    /// <summary>下一次 PutAsync 抛出的异常（置 null 恢复）。用于模拟 S3 写入失败。</summary>
    public Exception? PutException;

    public void Reset()
    {
        _objects.Clear();
        DeleteCallCount = 0;
        DeleteOverride = null;
        PutException = null;
    }

    public IReadOnlyCollection<string> Keys => _objects.Keys.ToList();

    public byte[]? GetBytes(string key)
        => _objects.TryGetValue(key, out var bytes) ? bytes : null;

    public async Task<ObjectStoragePutResult> PutAsync(
        string key, Stream content, long? contentLength,
        string contentType, CancellationToken ct = default)
    {
        if (PutException is not null) throw PutException;

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        _objects[key] = ms.ToArray();
        return new ObjectStoragePutResult(key, ms.Length, $"etag-{key}");
    }

    public Task<ObjectStorageGetResult> GetAsync(
        string key, long? start = null, long? end = null,
        CancellationToken ct = default)
    {
        if (!_objects.TryGetValue(key, out var bytes))
            throw new ObjectNotFoundException(key);

        var total = (long)bytes.Length;

        if (!start.HasValue && !end.HasValue)
        {
            return Task.FromResult(new ObjectStorageGetResult(
                new MemoryStream(bytes, writable: false), total, total, null));
        }

        long s, e;
        if (start.HasValue && end.HasValue)
        {
            s = start.Value;
            e = Math.Min(end.Value, total - 1);
        }
        else if (start.HasValue)
        {
            s = start.Value;
            e = total - 1;
        }
        else
        {
            // 与 S3ObjectStorage 的 ByteRange(0, end) 一致：取前 end+1 字节
            s = 0;
            e = Math.Min(end!.Value, total - 1);
        }

        if (s >= total || e < s)
            throw new RangeNotSatisfiableException();

        var length = e - s + 1;
        var slice = new byte[length];
        Array.Copy(bytes, s, slice, 0, length);

        var result = new ObjectStorageGetResult(
            new MemoryStream(slice, writable: false),
            length,
            total,
            $"bytes {s}-{e}/{total}");

        return Task.FromResult(result);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        Interlocked.Increment(ref DeleteCallCount);

        if (DeleteOverride is not null)
            return await DeleteOverride(key);

        return _objects.TryRemove(key, out _);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_objects.ContainsKey(key));
}
