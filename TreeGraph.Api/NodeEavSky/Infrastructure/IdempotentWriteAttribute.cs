using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace TreeGraph.Api.NodeEavSky.Infrastructure;

/// <summary>
/// 写入幂等指纹过滤器（自 APromisedLand DiberyTree 的 AttributeService 幂等方案迁移）。
///
/// 机制：
///   1. 对「HTTP 方法 + 请求路径 + 乐观锁头(X-Expected-Updated-At) + 原始请求体」
///      计算 SHA256 指纹；
///   2. 指纹首次出现：写入 30 秒占位符后放行。窗口内重复请求：
///      - 首次请求仍在处理（占位符）→ 直接 409，拒绝重复提交；
///      - 首次请求已完成（快照）→ 原样重放首次响应（同状态码、同响应体），不再执行业务；
///   3. 请求抛未处理异常或产生 5xx 时移除占位符，允许用户修正后重试。
///
/// 注意：必须在 Resource 阶段（IAsyncResourceFilter）读取请求体——Action 阶段晚于
/// [FromBody] 模型绑定，此时请求流已被消费完毕，EnableBuffering 也无法重读，
/// 会导致所有请求指纹退化为「方法+路径」而错误碰撞。
///
/// 与 ApiEnvelopeFilter 的配合：本过滤器 Order=2，结果阶段晚于信封过滤器（Order=0），
/// 缓存的是已包装 ApiResponse 的最终响应，重放时直接返回；信封过滤器识别到已是信封则跳过。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IdempotentWriteAttribute : Attribute,
    IAsyncResourceFilter, IResultFilter, IExceptionFilter, IOrderedFilter
{
    /// <summary>幂等窗口（秒），默认 30 秒（与 DiberyTree 方案一致）。</summary>
    public int WindowSeconds { get; set; } = 30;

    public int Order => 2;

    private const string CachePrefix = "eav:idem:";
    private const string IndexPrefix = "eav:idem:idx:";
    private static readonly string CacheKeyItem = CachePrefix + "key";
    private static readonly string ReplayItem = CachePrefix + "replay";
    private static readonly string DeletePrefixItem = CachePrefix + "delete-prefix";
    private const string ExpectedUpdatedAtHeader = "X-Expected-Updated-At";

    private sealed class InFlightPlaceholder
    {
        public static readonly InFlightPlaceholder Instance = new();
    }

    private sealed record ResponseSnapshot(int StatusCode, object? Value);

    /// <summary>同一资源前缀下登记过的幂等键，供 DELETE 成功后整体淘汰。</summary>
    private sealed class KeyIndex
    {
        public readonly object Gate = new();
        public readonly HashSet<string> Keys = new();
    }

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        request.EnableBuffering();

        string body;
        using (var reader = new StreamReader(
                   request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
                   leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
        }
        request.Body.Position = 0;

        var expectedVersion = request.Headers.TryGetValue(
            ExpectedUpdatedAtHeader, out var values)
            ? values.ToString()
            : string.Empty;
        var resourcePrefix = ResourcePrefix(request.Path);
        var cache = context.HttpContext.RequestServices
            .GetRequiredService<IMemoryCache>();

        // DELETE 本身不做幂等去重；成功后淘汰同资源下的写入快照，
        // 保证「删除后立即用同载荷重建」不会被旧快照重放。
        if (HttpMethods.IsDelete(request.Method))
        {
            context.HttpContext.Items[DeletePrefixItem] = resourcePrefix;
            await next();
            return;
        }

        var fingerprint = Fingerprint(
            request.Method, request.Path, expectedVersion, body);
        var cacheKey = CachePrefix + fingerprint;

        if (cache.TryGetValue(cacheKey, out var boxed))
        {
            if (boxed is ResponseSnapshot snapshot)
            {
                // 已完成：重放首次响应（信封已包装，ApiEnvelopeFilter 会自动跳过）
                context.Result = new ObjectResult(snapshot.Value)
                {
                    StatusCode = snapshot.StatusCode
                };
                context.HttpContext.Items[ReplayItem] = true;
                // 资源过滤器短路时不能再调用 next()：MVC 会直接执行结果管道
                return;
            }

            // 占位符：首次请求仍在处理
            context.Result = new ObjectResult(
                new { error = "请求正在处理中，请勿重复提交" })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
            context.HttpContext.Items[ReplayItem] = true;
            return;
        }

        // 占位符：阻止窗口内并发重复提交
        cache.Set(cacheKey, InFlightPlaceholder.Instance, TimeSpan.FromSeconds(WindowSeconds));
        context.HttpContext.Items[CacheKeyItem] = cacheKey;
        RegisterKey(cache, resourcePrefix, cacheKey, WindowSeconds);

        await next();
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        var cache = context.HttpContext.RequestServices
            .GetRequiredService<IMemoryCache>();

        // DELETE 成功（2xx）：淘汰该资源前缀下的全部写入快照/占位符
        if (context.HttpContext.Items[DeletePrefixItem] is string deletePrefix)
        {
            if (StatusCodeOf(context.Result) is >= 200 and < 300)
                EvictPrefix(cache, deletePrefix);
            return;
        }

        // 重放/拦截产生的结果不再回写缓存
        if (context.HttpContext.Items.ContainsKey(ReplayItem)) return;
        if (context.HttpContext.Items[CacheKeyItem] is not string cacheKey) return;

        int statusCode;
        object? value;
        switch (context.Result)
        {
            case ObjectResult obj:
                statusCode = obj.StatusCode ?? StatusCodes.Status200OK;
                value = obj.Value;
                break;
            case StatusCodeResult sc:
                statusCode = sc.StatusCode;
                value = null;
                break;
            default:
                // 非标准结果（如原始 ContentResult），不缓存
                cache.Remove(cacheKey);
                return;
        }

        if (statusCode >= 500)
        {
            // 服务端错误不缓存，允许立即重试
            cache.Remove(cacheKey);
            return;
        }

        // 业务结果（含 4xx，与 DiberyTree 方案一致：同载荷重复提交得到相同错误）
        cache.Set(cacheKey, new ResponseSnapshot(statusCode, value),
            TimeSpan.FromSeconds(WindowSeconds));
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        // 无需处理
    }

    public void OnException(ExceptionContext context)
    {
        // 未处理异常：移除占位符，避免 30 秒内无法重试
        // （若异常被 ApiEnvelopeFilter 处理为响应，结果阶段会重新写入快照）
        if (context.HttpContext.Items[CacheKeyItem] is string cacheKey)
        {
            context.HttpContext.RequestServices
                .GetRequiredService<IMemoryCache>()
                .Remove(cacheKey);
        }
    }

    private static string Fingerprint(
        string method, string path, string expectedVersion, string body)
    {
        var raw = string.Concat(
            method, "\n", path, "\n", expectedVersion, "\n", body);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// 资源前缀：路径倒数第二段为 entities/rows 时去掉末段（单个资源 id/type/rowId），
    /// 使「写入资源」与「删除该资源」落到同一前缀下。
    /// 例：.../entities/{id} → .../entities；.../tables/{t}/rows/{rowId} → .../tables/{t}/rows。
    /// </summary>
    private static string ResourcePrefix(Microsoft.AspNetCore.Http.PathString path)
    {
        var value = path.HasValue ? path.Value! : "/";
        var lastSlash = value.LastIndexOf('/');
        if (lastSlash <= 0) return value;

        var parent = value.AsSpan(0, lastSlash);
        var parentSlash = parent.LastIndexOf('/');
        var lastSegment = parentSlash >= 0
            ? parent.Slice(parentSlash + 1)
            : parent;

        return lastSegment is "entities" or "rows"
            ? value[..lastSlash]
            : value;
    }

    private static void RegisterKey(
        IMemoryCache cache, string resourcePrefix, string cacheKey, int windowSeconds)
    {
        var indexKey = IndexPrefix + resourcePrefix;
        var index = cache.GetOrCreate(indexKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow =
                TimeSpan.FromSeconds(windowSeconds + 5); // 略长于幂等窗口
            return new KeyIndex();
        })!;

        lock (index.Gate)
        {
            index.Keys.Add(cacheKey);
        }
    }

    private static void EvictPrefix(IMemoryCache cache, string resourcePrefix)
    {
        var indexKey = IndexPrefix + resourcePrefix;
        if (cache.Get(indexKey) is not KeyIndex index) return;

        string[] keys;
        lock (index.Gate)
        {
            keys = index.Keys.ToArray();
            index.Keys.Clear();
        }

        foreach (var key in keys)
            cache.Remove(key);
        cache.Remove(indexKey);
    }

    private static int? StatusCodeOf(IActionResult result) => result switch
    {
        ObjectResult obj => obj.StatusCode ?? StatusCodes.Status200OK,
        StatusCodeResult sc => sc.StatusCode,
        _ => null
    };
}
