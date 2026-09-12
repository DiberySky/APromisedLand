namespace APromisedLand.AppHost.Extensions;

public static class PostgresExtension
{
    public static IDistributedApplicationBuilder AddPostgres(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // Postgres：宿主机 8433 不常见，保留
        // pgAdmin：宿主机端口改为 15050，避开默认 5050 冲突
        resourceContext.Postgres = builder.AddPostgres("Postgres", port: 8433)
            .WithDataVolume("postgres-data")
            .WithPgAdmin(pg => pg.WithHostPort(15050))   // ★ 原默认 5050 → 15050
            .WithOtlpExporter();

        // 创建具体数据库
        resourceContext.FileTransDb = resourceContext.Postgres.AddDatabase("fileTransDb");
        resourceContext.TreeDb = resourceContext.Postgres.AddDatabase("TreeDb");
        resourceContext.HangfireDb = resourceContext.Postgres.AddDatabase("HangfireDb");
        resourceContext.MetadataDb = resourceContext.Postgres.AddDatabase("MetadataDb");
        resourceContext.VectorAdminDb = resourceContext.Postgres.AddDatabase("vectorAdminDb");
        // resourceContext.MafRagDb = resourceContext.Postgres.AddDatabase("MafRagDb");

        return builder;
    }
}