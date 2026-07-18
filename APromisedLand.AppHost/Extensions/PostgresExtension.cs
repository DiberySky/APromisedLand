namespace APromisedLand.AppHost.Extensions;

public static class PostgresExtension
{
    public static IDistributedApplicationBuilder AddPostgres(
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
        context.TreeDb = context.Postgres.AddDatabase("TreeDb");
        
        return builder;
    }
}