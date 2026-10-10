using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Infrastructure;

/// <summary>
/// 全局结果/异常过滤器：为 NodeEavSky 所有控制器统一包装 ApiResponse 信封
/// （自 APromisedLand 的 ApiResponse 方案迁移）。
///
/// - 2xx：{ success=true, message=null, data=原始返回体 }
/// - 4xx/5xx：{ success=false, message=错误说明, data=原始错误体（结构化 errors 等） }
/// - ContentResult（如 /json 原始预览）不包装，保持原始响应。
/// - StringTreeSky 等其它模块控制器不受影响。
///
/// Order=0：结果阶段先于 IdempotentWriteAttribute（Order=2）执行，
/// 幂等过滤器缓存的是已包装的最终响应。
/// </summary>
public sealed class ApiEnvelopeFilter : IResultFilter, IExceptionFilter, IOrderedFilter
{
    public int Order => 0;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private static bool AppliesTo(FilterContext context)
        => context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor cad
           && cad.ControllerTypeInfo.Namespace?.Contains(
               "NodeEavSky.Controllers", StringComparison.Ordinal) == true;

    // ------------------------------------------------------------
    // 结果包装
    // ------------------------------------------------------------

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!AppliesTo(context)) return;

        switch (context.Result)
        {
            case ObjectResult obj:
                if (IsEnvelope(obj.Value)) return;   // 幂等重放等场景，已包装
                WrapObjectResult(context, obj);
                break;

            case StatusCodeResult sc:
                context.Result = new ObjectResult(BuildStatusEnvelope(sc.StatusCode))
                {
                    StatusCode = sc.StatusCode
                };
                break;

            // ContentResult（/json 原始 JSON 预览）、EmptyResult 等保持原样
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
            case EavValidationException ex:
                context.Result = new ObjectResult(
                    ApiResponse<object>.Fail("数据校验失败", new { errors = ex.Errors }))
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
                context.ExceptionHandled = true;
                break;

            case EavConcurrencyException ex:
                context.Result = new ObjectResult(
                    ApiResponse<object>.Fail(
                        "并发冲突：实体已被其他用户修改，请刷新后重试",
                        new
                        {
                            currentUpdatedAt = ex.CurrentUpdatedAt,
                            expectedUpdatedAt = ex.ExpectedUpdatedAt
                        }))
                {
                    StatusCode = StatusCodes.Status409Conflict
                };
                context.ExceptionHandled = true;
                break;

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
