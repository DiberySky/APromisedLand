using System.Diagnostics;
using Hangfire.Server;

namespace MAFRagService.Startup.Diagnostics;

/// <summary>
/// Hangfire 服务端过滤器：为每个后台作业生成一个 OpenTelemetry Span。
///
/// 行为：
///   · OnPerforming → 开启 Activity（Kind = Consumer，语义上"消费队列消息"）
///   · OnPerformed  → 打标签（异常 / 成功）、关闭 Activity
///
/// 标签（tags）：
///   · hangfire.job_id   背景作业 ID
///   · hangfire.job_type 目标类型全名
///   · hangfire.job_meth 目标方法名
///   · hangfire.queue    队列名
///   · hangfire.retries  已重试次数（job 参数 RetryCount，缺省 0）
///   · exception.*       异常时填充
///
/// 注意：
///   · RetryCount 通过 PerformContext.GetJobParameter&lt;int&gt;("RetryCount") 读取，
///     这是 Hangfire 官方约定（AutomaticRetryAttribute 内部同样使用该键）。
///   · Activity 存放在 PerformContext.Items 中，键使用 <see cref="ItemKey"/>，
///     避免与其他过滤器冲突。
///   · 使用同步 <see cref="IServerFilter"/>，与 Hangfire 1.7.x 兼容。
/// </summary>
public sealed class HangfireTelemetryFilter : IServerFilter
{
    private const string ItemKey   = "__mafrag_hangfire_activity";
    private const string RetryKey  = "RetryCount";

    public void OnPerforming(PerformingContext filterContext)
    {
        ArgumentNullException.ThrowIfNull(filterContext);

        var job = filterContext.BackgroundJob.Job;

        var activity = MAFRagActivity.Source.StartActivity(
            name: $"Hangfire {job.Type.Name}.{job.Method.Name}",
            kind: ActivityKind.Consumer);

        if (activity is null) return;

        activity.SetTag("hangfire.job_id",   filterContext.BackgroundJob.Id);
        activity.SetTag("hangfire.job_type", job.Type.FullName);
        activity.SetTag("hangfire.job_meth", job.Method.Name);
        activity.SetTag("hangfire.queue",    job.Queue ?? "default");

        // ★ 修正点：不再访问不存在的 Job.Parameters，改用 GetJobParameter<int>
        activity.SetTag("hangfire.retries",  ReadRetryCount(filterContext));

        filterContext.Items[ItemKey] = activity;
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        ArgumentNullException.ThrowIfNull(filterContext);

        if (!filterContext.Items.TryGetValue(ItemKey, out var raw) ||
            raw is not Activity activity)
        {
            return;
        }

        if (filterContext.Exception is { } ex)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.SetTag("exception.type",       ex.GetType().FullName);
            activity.SetTag("exception.message",    ex.Message);
            activity.SetTag("exception.stacktrace", ex.StackTrace);
        }
        else
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        activity.Dispose();
        filterContext.Items.Remove(ItemKey);
    }

    // ------------------------------------------------------------
    // 读取 RetryCount：通过 PerformContext.GetJobParameter<T>
    //   · 该扩展方法来自 Hangfire 的 PerformContextExtensions
    //   · 键名固定为 "RetryCount"（与 AutomaticRetryAttribute 保持一致）
    //   · 首次执行时参数可能不存在，返回 0
    // ------------------------------------------------------------
    private static int ReadRetryCount(PerformContext ctx)
    {
        try
        {
            return ctx.GetJobParameter<int>(RetryKey);
        }
        catch
        {
            // 参数缺失 / 类型不匹配时降级为 0，不影响主流程
            return 0;
        }
    }
}