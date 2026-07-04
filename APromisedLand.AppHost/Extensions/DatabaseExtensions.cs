namespace APromisedLand.AppHost.Extensions;

public static class DatabaseExtensions
{
    public static IDistributedApplicationBuilder AddDatabases(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // Postgres
        context.Postgres = builder.AddPostgres("Postgres", port: 8433)
            .WithDataVolume("postgres-data")
            .WithPgAdmin()
            .WithOtlpExporter();

        // 创建具体数据库
        context.QuestionDb = context.Postgres.AddDatabase("questionDb");
        context.FileTransDb = context.Postgres.AddDatabase("fileTransDb");

        // Redis
        context.Redis = builder.AddRedis("Redis")
            .WithDataVolume("redis-data", isReadOnly: false);

        // RabbitMQ
        context.RabbitMq = builder.AddRabbitMQ("Messaging")
            .WithDataVolume("rabbitmq-data")
            .WithManagementPlugin(port: 15672);

        return builder;
    }
}