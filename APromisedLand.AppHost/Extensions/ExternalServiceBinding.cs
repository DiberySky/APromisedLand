using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace APromisedLand.AppHost.Extensions;

internal sealed record ExternalServiceBinding(
    IResourceBuilder<IResourceWithEndpoints>? Resource,
    string EndpointName,
    string ConnectionStringName,
    string? SchemeOverride)
{
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

    public void Apply(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> consumer,
        ILogger logger)
    {
        if (Resource is null)
        {
            logger.LogWarning(
                "跳过 ConnectionStrings__{Name} 注入：{Consumer} 未配置该外部资源。",
                ConnectionStringName, consumer.Resource.Name);
            return;
        }

        var resource = Resource.Resource;

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

        logger.LogInformation(
            "已注入 {Consumer} → ConnectionStrings__{Name}（endpoint={Endpoint}, scheme={Scheme}）",
            consumer.Resource.Name,
            ConnectionStringName,
            EndpointName,
            SchemeOverride ?? "(auto)");
    }
}