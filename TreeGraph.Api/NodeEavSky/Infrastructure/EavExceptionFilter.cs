using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Infrastructure;

/// <summary>核心 EAV 写业务专属异常映射；通用响应信封由 WebShared 提供。</summary>
public sealed class EavExceptionFilter : IExceptionFilter, IOrderedFilter
{
    public int Order => -1;

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled
            || context.ActionDescriptor is not Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor cad
            || cad.ControllerTypeInfo.Namespace?.Contains("NodeEavSky.Controllers", StringComparison.Ordinal) != true)
            return;

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
        }
    }
}
