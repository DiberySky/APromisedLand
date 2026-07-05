namespace APromisedLand.AppHost.Extensions;

public static class RabbitMqExtensions
{
    public static IDistributedApplicationBuilder AddRabbitMq(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // RabbitMQ
        context.RabbitMq = builder.AddRabbitMQ("RabbitMQ")
            .WithDataVolume("rabbitmq-data")
            .WithManagementPlugin(port: 15672);

        return builder;
    }
}