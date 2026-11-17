using Microsoft.EntityFrameworkCore;
using TreeGraph.Persistence.Data;
using Wolverine.EntityFrameworkCore;

namespace TreeGraph.Api.MessagingSky;

/// <summary>
/// 核心 HTTP 写请求统一事务：业务 SaveChanges 与 Outbox 记录共享同一连接和事务，
/// 提交后才执行旁路动作并尝试 flush。flush 失败不影响已提交请求，持久化发送代理会继续派发。
/// </summary>
public sealed class TreeGraphWriteTransactionMiddleware(
    RequestDelegate next, ILogger<TreeGraphWriteTransactionMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        TreeGraphDbContext db,
        IDbContextOutbox outbox,
        TreeGraphRequestTransaction requestTransaction)
    {
        if (!IsWrite(context.Request.Method))
        {
            await next(context);
            return;
        }

        requestTransaction.BeginManaged();
        outbox.Enroll(db);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(context.RequestAborted);
            await next(context);

            if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
            {
                await transaction.RollbackAsync(context.RequestAborted);
                return;
            }

            await db.SaveChangesAsync(context.RequestAborted);
            await transaction.CommitAsync(context.RequestAborted);
        });

        if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
            return;

        await requestTransaction.RunPostCommitAsync(context.RequestAborted);

        try
        {
            await outbox.FlushOutgoingMessagesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Outbox 即时派发失败，已持久化消息将由后台发送代理重试");
        }
    }

    private static bool IsWrite(string method)
        => !HttpMethods.IsGet(method)
        && !HttpMethods.IsHead(method)
        && !HttpMethods.IsOptions(method)
        && !HttpMethods.IsTrace(method);
}
