using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MAFWorkFlowApi.Infrastructure;

/// <summary>
/// 统一异常映射中间件。
/// ★ 关键修复：原实现用 when (ex.StatusCode is not null) 过滤，
///   导致网络层 HttpRequestException（连接被拒、响应提前结束、DNS 失败等）
///   直接冒泡到 DeveloperExceptionPage，堆栈暴露给客户端。
///   现在：所有 HttpRequestException 都被捕获；无 StatusCode → 502。
/// </summary>
public sealed class LiteGraphExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<LiteGraphExceptionMiddleware> _logger;

    public LiteGraphExceptionMiddleware(
        RequestDelegate next,
        ILogger<LiteGraphExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    [DebuggerDisableUserUnhandledExceptions]
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        // ★ 捕获所有 HttpRequestException，不再用 when 过滤
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode is not null ? (int)ex.StatusCode.Value : 0;

            if (statusCode == 0)
            {
                // ── 网络层错误：连接被拒、响应提前结束、DNS 失败、TLS 失败等 ──
                _logger.LogError(ex,
                    "LiteGraph 网络调用失败：{Message}（Inner={Inner}）",
                    ex.Message,
                    ex.InnerException?.GetType().Name ?? "none");

                await WriteErrorAsync(
                    context,
                    StatusCodes.Status502BadGateway,
                    "LiteGraph 服务不可用",
                    ex.Message);
                return;
            }

            // ── 上游返回了非 2xx 状态码 ──
            _logger.LogError(ex,
                "LiteGraph 返回错误状态码 {Code}：{Message}",
                statusCode, ex.Message);

            var mappedStatusCode = statusCode switch
            {
                400 => StatusCodes.Status400BadRequest,
                401 => StatusCodes.Status401Unauthorized,
                403 => StatusCodes.Status403Forbidden,
                404 => StatusCodes.Status404NotFound,
                409 => StatusCodes.Status409Conflict,
                500 => StatusCodes.Status502BadGateway,
                502 => StatusCodes.Status502BadGateway,
                503 => StatusCodes.Status503ServiceUnavailable,
                504 => StatusCodes.Status504GatewayTimeout,
                _   => StatusCodes.Status500InternalServerError
            };

            if (context.Response.HasStarted) return;

            context.Response.StatusCode = mappedStatusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            await context.Response.WriteAsJsonAsync(new
            {
                error = ex.Message,
                code = statusCode,
                traceId = context.TraceIdentifier
            }, JsonOpts);
        }
        // ★ 超时（客户端未取消）
        catch (TaskCanceledException ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "LiteGraph 调用超时");
            await WriteErrorAsync(
                context,
                StatusCodes.Status504GatewayTimeout,
                "LiteGraph 调用超时",
                "请求超时，请稍后重试");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context, int statusCode, string error, string? detail)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(new
        {
            error,
            detail,
            traceId = context.TraceIdentifier
        }, JsonOpts);
    }
}