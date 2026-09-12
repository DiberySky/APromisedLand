using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost.Extensions;

internal static class ResourceBuilderExtensions
{
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