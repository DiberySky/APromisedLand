using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost;

public sealed class LiteGraphResource(string name)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "rest-api";

    private EndpointReference? _httpEndpoint;
    public EndpointReference HttpEndpoint
        => _httpEndpoint ??= new(this, HttpEndpointName);

    public ReferenceExpression ConnectionStringExpression
        => ReferenceExpression.Create(
            $"http://{HttpEndpoint.Property(EndpointProperty.Host)}:{HttpEndpoint.Property(EndpointProperty.Port)}");
}