using Aspire.Hosting.Foundry;

namespace APromisedLand.AppHost.Extensions;

public static class FoundryExtension
{
    public static IDistributedApplicationBuilder AddFoundry(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {

        builder.AddProject<Projects.FoundryLocalService>("AIFoundryLocal")
            .WithOtlpExporter();

        return builder;
    }
}