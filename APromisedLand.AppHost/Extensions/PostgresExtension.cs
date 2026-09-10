namespace APromisedLand.AppHost.Extensions;

public static class PostgresExtension
{
    public static IDistributedApplicationBuilder AddPostgres(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // Postgres：宿主机 8433 不常见，保留
        // pgAdmin：宿主机端口改为 15050，避开默认 5050 冲突
        context.Postgres = builder.AddPostgres("Postgres", port: 8433)
            .WithDataVolume("postgres-data")
            .WithPgAdmin(pg => pg.WithHostPort(15050))   // ★ 原默认 5050 → 15050
            .WithOtlpExporter();

        // 创建具体数据库
        context.FileTransDb = context.Postgres.AddDatabase("fileTransDb");
        context.TreeDb = context.Postgres.AddDatabase("TreeDb");
        context.HangfireDb = context.Postgres.AddDatabase("HangfireDb");
        context.MetadataDb = context.Postgres.AddDatabase("MetadataDb");
        context.VectorAdminDb = context.Postgres.AddDatabase("vectorAdminDb");
        // context.MafRagDb = context.Postgres.AddDatabase("MafRagDb");

        return builder;
    }
}