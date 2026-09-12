using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// 描述一次「外部服务 → 消费方」的 ConnectionStrings__ 注入。
/// 用于承载非 Aspire 原生协议的服务（Nebula / Weaviate / SeaweedFS 等）。
/// </summary>
internal sealed record ExternalServiceBinding(
    IResourceBuilder<IResourceWithEndpoints>? Resource,
    string EndpointName,
    string ConnectionStringName,
    string? SchemeOverride)
{
    /// <summary>
    /// 泛型构造：自动完成 IResourceBuilder&lt;T&gt; → IResourceBuilder&lt;IResourceWithEndpoints&gt;
    /// 的协变转换。
    /// </summary>
    public static ExternalServiceBinding From<T>(
        IResourceBuilder<T>? resource,
        string endpointName,
        string connectionStringName,
        string? schemeOverride = null)
        where T : class, IResource, IResourceWithEndpoints
        => new(
            resource as IResourceBuilder<IResourceWithEndpoints>,
            endpointName,
            connectionStringName,
            schemeOverride);

    /// <summary>
    /// 执行注入：校验 endpoint → 写环境变量 → 建立 WaitFor → 记录日志。
    /// </summary>
    public void Apply(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> consumer,
        ILogger logger)
    {
        // ─── 可选依赖：未配置则告警跳过 ─────────────────────────────
        if (Resource is null)
        {
            logger.LogWarning(
                "跳过 ConnectionStrings__{Name} 注入：{Consumer} 未配置该外部资源。",
                ConnectionStringName, consumer.Resource.Name);
            return;
        }

        var resource = Resource.Resource;

        // ─── Fail-Fast：endpoint 名称写错时立即暴露 ──────────────────
        var declaredEndpoints = resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(e => e.Name)
            .ToList();

        if (!declaredEndpoints.Contains(EndpointName))
        {
            throw new InvalidOperationException(
                $"资源 '{resource.Name}' 未声明 endpoint '{EndpointName}'。" +
                $"已声明的 endpoint：{string.Join(", ", declaredEndpoints)}。" +
                $"无法为 '{consumer.Resource.Name}' 注入 '{ConnectionStringName}'。");
        }

        var ep = Resource.GetEndpoint(EndpointName);

        // ─── scheme：显式覆盖优先，否则使用 EndpointProperty.Url ──────
        // 注意：EndpointProperty.Scheme 返回 EndpointReferenceExpression，
        //       不能与 string? 直接使用 ??。此处分支处理避免类型冲突。
        if (SchemeOverride is not null)
        {
            consumer.WithEnvironment(
                $"ConnectionStrings__{ConnectionStringName}",
                $"{SchemeOverride}://{ep.Property(EndpointProperty.HostAndPort)}");
        }
        else
        {
            consumer.WithEnvironment(
                $"ConnectionStrings__{ConnectionStringName}",
                ep.Property(EndpointProperty.Url));
        }

        consumer.WaitFor(Resource);

        // ─── 结构化日志 ──────────────────────────────────────────────
        logger.LogInformation(
            "已注入 {Consumer} → ConnectionStrings__{Name}（endpoint={Endpoint}, scheme={Scheme}）",
            consumer.Resource.Name,
            ConnectionStringName,
            EndpointName,
            SchemeOverride ?? "(auto)");
    }
}