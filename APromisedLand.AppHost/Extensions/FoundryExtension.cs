using Aspire.Hosting.Foundry;

namespace APromisedLand.AppHost.Extensions;

public static class FoundryExtension
{
    public static IDistributedApplicationBuilder AddFoundry(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {

        builder.AddProject<Projects.FoundryLocalService>("AIFoundryLocal")
            .WithOtlpExporter();

        return builder;
    }
}