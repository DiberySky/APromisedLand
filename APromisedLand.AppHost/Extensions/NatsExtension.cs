namespace APromisedLand.AppHost.Extensions;

public static class NatsExtension
{
    public static IDistributedApplicationBuilder AddNats(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Nats = builder.AddNats("Nats")
            .WithHttpEndpoint(8222, 8222, name: "nats")
            .WithJetStream()
            .WithDataVolume("nats-data",isReadOnly: false)
            .WithOtlpExporter();

        return builder;
    }
}