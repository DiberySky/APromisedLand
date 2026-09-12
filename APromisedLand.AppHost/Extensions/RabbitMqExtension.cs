namespace APromisedLand.AppHost.Extensions;

public static class RabbitMqExtension
{
    public static IDistributedApplicationBuilder AddRabbitMq(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // RabbitMQ
        resourceContext.RabbitMq = builder.AddRabbitMQ("RabbitMQ")
            .WithDataVolume("rabbitmq-data")
            .WithManagementPlugin(port: 15672);

        return builder;
    }
}