namespace APromisedLand.AppHost.Extensions;

public static class RedisExtension
{
    public static IDistributedApplicationBuilder AddRedis(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // Redis
        context.Redis = builder.AddRedis("Redis")
            .WithDataVolume("redis-data", isReadOnly: false);

        return builder;
    }
}