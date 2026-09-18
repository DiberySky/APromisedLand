namespace MAFWorkFlowApi.Infrastructure;

public sealed class LiteGraphExceptionMiddleware(
    RequestDelegate next,
    ILogger<LiteGraphExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            logger.LogError(ex, "LiteGraph HTTP operation failed");

            var upstreamCode = (int)ex.StatusCode.Value;

            context.Response.StatusCode = upstreamCode switch
            {
                400 => StatusCodes.Status400BadRequest,
                401 => StatusCodes.Status401Unauthorized,
                403 => StatusCodes.Status403Forbidden,
                404 => StatusCodes.Status404NotFound,
                409 => StatusCodes.Status409Conflict,
                500 => StatusCodes.Status502BadGateway,
                _   => StatusCodes.Status500InternalServerError
            };

            await context.Response.WriteAsJsonAsync(new
            {
                error = ex.Message,
                code = upstreamCode
            });
        }
    }
}