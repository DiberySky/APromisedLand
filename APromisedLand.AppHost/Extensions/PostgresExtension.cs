namespace APromisedLand.AppHost.Extensions;

public static class PostgresExtension
{
    public static IDistributedApplicationBuilder AddPostgres(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        resourceContext.Postgres = builder.AddPostgres("Postgres", port: 8433)
            .WithDataVolume("postgres-data")
            .WithPgAdmin(pg => pg.WithHostPort(15050));

        resourceContext.FileTransDb = resourceContext.Postgres.AddDatabase("fileTransDb");
        resourceContext.TreeDb = resourceContext.Postgres.AddDatabase("TreeDb");
        resourceContext.HangfireDb = resourceContext.Postgres.AddDatabase("HangfireDb");
        resourceContext.MetadataDb = resourceContext.Postgres.AddDatabase("MetadataDb");
        resourceContext.VectorAdminDb = resourceContext.Postgres.AddDatabase("vectorAdminDb");

        return builder;
    }
}