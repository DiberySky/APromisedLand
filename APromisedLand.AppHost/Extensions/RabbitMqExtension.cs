namespace APromisedLand.AppHost.Extensions;

public static class RabbitMqExtension
{
    public static IDistributedApplicationBuilder AddRabbitMq(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // 本机离线验证使用已有镜像，避免 Aspire 17 默认版本触发联网拉取。
        resourceContext.RabbitMq = builder.AddRabbitMQ("RabbitMQ")
            .WithImageTag("4.3-management")
            .WithDataVolume("rabbitmq-data")
            .WithManagementPlugin(port: 15672);

        return builder;
    }
}