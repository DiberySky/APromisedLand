namespace APromisedLand.AppHost.Extensions;

public static class RedisExtension
{
    public static IDistributedApplicationBuilder AddRedis(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // Redis
        context.Redis = builder.AddRedis("Redis")
            .WithPersistence()
            .WithDataVolume("redis-data", isReadOnly: false)
            .WithOtlpExporter();

        return builder;
    }
}