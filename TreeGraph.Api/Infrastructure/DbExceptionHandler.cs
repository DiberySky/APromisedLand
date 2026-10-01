using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TreeGraph.Api.Infrastructure;

/// <summary>把 EF Core / Npgsql 的已知异常映射为合适的 HTTP 响应</summary>
public sealed class DbExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DbExceptionHandler> _logger;

    public DbExceptionHandler(ILogger<DbExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception ex, CancellationToken ct)
    {
        if (ex is not DbUpdateException due
            || due.InnerException is not PostgresException pg)
            return false;

        if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _logger.LogWarning("唯一约束冲突: {Constraint}", pg.ConstraintName);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "资源已存在",
                constraint = pg.ConstraintName
            }, ct);
            return true;
        }

        if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            _logger.LogWarning("外键约束冲突: {Constraint}", pg.ConstraintName);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "外键约束冲突",
                constraint = pg.ConstraintName
            }, ct);
            return true;
        }

        return false;
    }
}
