using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost.Extensions;

internal static class ResourceBuilderExtensions
{
    /// <summary>
    /// 若 <paramref name="provider"/> 不为 null，则：
    ///   1. <c>WithReference</c> —— 建立引用（注入连接信息、服务发现、Dashboard 连线）
    ///   2. <c>WaitFor</c>       —— 建立启动依赖顺序（可由 waitFor 参数关闭）
    /// 否则静默返回，保持链式调用不中断。
    ///
    /// 语义定位：<b>可选依赖</b>。真正的必需依赖请勿使用本方法——
    /// 缺失时应让 AppHost 启动直接失败，而不是被静默跳过。
    /// </summary>
    internal static IResourceBuilder<ProjectResource> WireIfPresent<T>(
        this IResourceBuilder<ProjectResource> consumer,
        IResourceBuilder<T>? provider,
        bool waitFor = true)
        where T : class, IResourceWithConnectionString
    {
        if (provider is null)
            return consumer;

        consumer.WithReference(provider);

        if (waitFor)
            consumer.WaitFor(provider);

        return consumer;
    }
}