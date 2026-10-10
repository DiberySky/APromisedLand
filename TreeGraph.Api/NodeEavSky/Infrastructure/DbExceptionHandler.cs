using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Infrastructure;

/// <summary>把 EF Core / Npgsql 的已知异常映射为合适的 HTTP 响应</summary>
public sealed class DbExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DbExceptionHandler> _logger;

    public DbExceptionHandler(ILogger<DbExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception ex, CancellationToken ct)
    {
        // ★ 修复：ExecuteUpdateAsync / ExecuteDeleteAsync 会抛出未经 DbUpdateException
        //   包装的裸 PostgresException，需要单独处理，否则会 500。
        var pg = ex switch
        {
            DbUpdateException due when due.InnerException is PostgresException inner => inner,
            PostgresException direct => direct,
            _ => null
        };

        if (pg is null) return false;

        if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _logger.LogWarning("唯一约束冲突: {Constraint}", pg.ConstraintName);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail("资源已存在",
                    new { constraint = pg.ConstraintName }), ct);
            return true;
        }

        if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            _logger.LogWarning("外键约束冲突: {Constraint}", pg.ConstraintName);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail("外键约束冲突",
                    new { constraint = pg.ConstraintName }), ct);
            return true;
        }

        // ★ 新增：CHECK 约束（如 ck_attr_int_no_unit / ck_composite_field_*）
        if (pg.SqlState == PostgresErrorCodes.CheckViolation)
        {
            _logger.LogWarning("CHECK 约束冲突: {Constraint}", pg.ConstraintName);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail("约束校验失败",
                    new { constraint = pg.ConstraintName }), ct);
            return true;
        }

        return false;
    }
}
