using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.MafWfApi.Infrastructure;

/// <summary>
/// 全局结果/异常过滤器：为本项目控制器统一包装 ApiResponse 信封
/// （方案照抄 TreeGraph.Api 的 ApiEnvelopeFilter）。
///
/// 生效范围：
/// - TreeGraph.MafWfApi.Controllers 命名空间下的所有控制器；
/// - 排除 HealthController（/api/health* 是 Aspire 探针协议，必须保持原样）。
///
/// 行为：
/// - 2xx：{ success=true, message=null, data=原始返回体 }
/// - 4xx/5xx：{ success=false, message=错误说明, data=原始错误体 }
/// - ContentResult / 文件流 / SSE（IAsyncEnumerable 不在 ObjectResult 阶段）不包装。
/// </summary>
public sealed class ApiEnvelopeFilter : IResultFilter, IExceptionFilter, IOrderedFilter
{
    public int Order => 0;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private static bool AppliesTo(FilterContext context)
    {
        if (context.ActionDescriptor is not Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor cad)
            return false;

        var ns = cad.ControllerTypeInfo.Namespace ?? string.Empty;
        if (!ns.Contains("TreeGraph.MafWfApi.Controllers", StringComparison.Ordinal))
            return false;

        // 健康探针保持裸响应
        return cad.ControllerTypeInfo.Name != "HealthController";
    }

    // ------------------------------------------------------------
    // 结果包装
    // ------------------------------------------------------------

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!AppliesTo(context)) return;

        switch (context.Result)
        {
            case ObjectResult obj:
                if (IsEnvelope(obj.Value)) return;   // 已包装（幂等重放等）
                WrapObjectResult(context, obj);
                break;

            case StatusCodeResult sc:
                context.Result = new ObjectResult(BuildStatusEnvelope(sc.StatusCode))
                {
                    StatusCode = sc.StatusCode
                };
                break;

            // ContentResult / EmptyResult / FileResult 等保持原样
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        // 无需处理
    }

    private static void WrapObjectResult(
        ResultExecutingContext context, ObjectResult obj)
    {
        var status = obj.StatusCode ?? StatusCodes.Status200OK;
        var success = status is >= 200 and < 300;

        ApiResponse<object> envelope = success
            ? (obj.Value is null
                ? ApiResponse.Ok()
                : ApiResponse<object>.Ok(obj.Value))
            : ApiResponse<object>.Fail(
                ExtractErrorMessage(obj.Value) ?? DefaultMessage(status),
                obj.Value);

        context.Result = new ObjectResult(envelope) { StatusCode = status };
    }

    private static bool IsEnvelope(object? value)
        => value is not null
           && value.GetType().IsGenericType
           && value.GetType().GetGenericTypeDefinition() == typeof(ApiResponse<>);

    private static ApiResponse<object> BuildStatusEnvelope(int status)
    {
        var success = status is >= 200 and < 300;
        return success
            ? ApiResponse.Ok()
            : ApiResponse.Fail(DefaultMessage(status));
    }

    private static string DefaultMessage(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "请求参数有误",
        StatusCodes.Status401Unauthorized => "未授权",
        StatusCodes.Status403Forbidden => "禁止访问",
        StatusCodes.Status404NotFound => "资源不存在",
        StatusCodes.Status409Conflict => "请求冲突",
        StatusCodes.Status500InternalServerError => "服务器内部错误",
        _ => $"请求失败（{status}）"
    };

    /// <summary>
    /// 从控制器现有的匿名错误体中提取人类可读信息：
    /// { error: "..." }、{ title/detail }、{ errors: [{field,message}] | {field:[...]} }。
    /// </summary>
    private static string? ExtractErrorMessage(object? payload)
    {
        if (payload is null) return null;

        try
        {
            using var doc = System.Text.Json.JsonSerializer.SerializeToDocument(
                payload, payload.GetType(), JsonOptions);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return null;

            if (root.TryGetProperty("error", out var errorProp)
                && errorProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return errorProp.GetString();
            }

            if (root.TryGetProperty("detail", out var detailProp)
                && detailProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return detailProp.GetString();
            }

            if (root.TryGetProperty("title", out var titleProp)
                && titleProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return titleProp.GetString();
            }

            if (root.TryGetProperty("errors", out var errorsProp))
            {
                var parts = new List<string>();

                if (errorsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in errorsProp.EnumerateArray())
                    {
                        if (item.ValueKind != System.Text.Json.JsonValueKind.Object) continue;

                        var field = item.TryGetProperty("field", out var f)
                            && f.ValueKind == System.Text.Json.JsonValueKind.String
                            ? f.GetString()
                            : null;
                        var message = item.TryGetProperty("message", out var m)
                            && m.ValueKind == System.Text.Json.JsonValueKind.String
                            ? m.GetString()
                            : item.GetRawText();

                        parts.Add(string.IsNullOrEmpty(field) ? message! : $"{field}: {message}");
                    }
                }
                else if (errorsProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    // ModelState 字典：{ 字段: [错误,...] }
                    foreach (var prop in errorsProp.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var msg in prop.Value.EnumerateArray())
                            {
                                if (msg.ValueKind == System.Text.Json.JsonValueKind.String)
                                    parts.Add($"{prop.Name}: {msg.GetString()}");
                            }
                        }
                    }
                }

                if (parts.Count > 0) return string.Join("; ", parts);
            }
        }
        catch
        {
            // 提取失败时回退到默认消息
        }

        return null;
    }

    // ------------------------------------------------------------
    // 异常映射（控制器未就地捕获时的兜底）
    // ------------------------------------------------------------

    public void OnException(ExceptionContext context)
    {
        if (!AppliesTo(context)) return;
        if (context.ExceptionHandled) return;

        switch (context.Exception)
        {
            case KeyNotFoundException ex:
                context.Result = new ObjectResult(ApiResponse.Fail(ex.Message))
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
                context.ExceptionHandled = true;
                break;

            case ArgumentException or InvalidOperationException:
                context.Result = new ObjectResult(
                    ApiResponse.Fail(context.Exception.Message))
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
                context.ExceptionHandled = true;
                break;
        }
    }
}
