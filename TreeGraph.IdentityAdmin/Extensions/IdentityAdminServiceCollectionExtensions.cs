using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TreeGraph.IdentityAdmin.Audit;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Backup;
using TreeGraph.IdentityAdmin.Bootstrap;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Revocation;
using TreeGraph.IdentityAdmin.StepUp;
using TreeGraph.IdentityAdmin.Token;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Extensions;

/// <summary>
/// 身份管理服务注册。仅在 TreeGraph.Blazor（服务端）调用。
/// </summary>
public static class IdentityAdminServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityAdmin(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<KeycloakAdminOptions>()
            .Bind(configuration.GetSection(KeycloakAdminOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AdminBaseUrl),
                "Keycloak:AdminBaseUrl 未配置")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Realm),
                "Keycloak:Realm 未配置");

        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(KeycloakClientNames.Token);
        services.AddHttpClient(KeycloakClientNames.Admin);

        services.AddDbContextFactory<IdentityAdminDbContext>((provider, builder) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("IdentityAdminDb")
                                   ?? "Host=localhost;Port=8433;Database=IdentityAdminDb;Username=postgres;Password=postgres";
            builder.UseNpgsql(connectionString);
        });

        services.AddSingleton<IAdminTokenProvider, AdminTokenProvider>();
        services.AddSingleton<KeycloakAdminClient>();
        services.AddSingleton<IAdminAuditStore, AdminAuditStore>();
        services.AddSingleton<IAdminAuditQueryService, AdminAuditQueryService>();
        services.AddSingleton<AdminOperationRunner>();
        services.AddSingleton<IIdentityRevocationStore, RedisIdentityRevocationStore>();
        services.AddSingleton<IStepUpService, StepUpService>();

        services.AddSingleton<IUsersAdminService, UsersAdminService>();
        services.AddSingleton<IRolesAdminService, RolesAdminService>();
        services.AddSingleton<IGroupsAdminService, GroupsAdminService>();
        services.AddSingleton<IClientsAdminService, ClientsAdminService>();
        services.AddSingleton<IClientScopesAdminService, ClientScopesAdminService>();
        services.AddSingleton<IIdentityProvidersAdminService, IdentityProvidersAdminService>();
        services.AddSingleton<IFederationAdminService, FederationAdminService>();
        services.AddSingleton<IAuthFlowsAdminService, AuthFlowsAdminService>();
        services.AddSingleton<IRealmSettingsAdminService, RealmSettingsAdminService>();
        services.AddSingleton<IEventsAdminService, EventsAdminService>();
        services.AddSingleton<ISessionsAdminService, SessionsAdminService>();
        services.AddSingleton<IExportAdminService, ExportAdminService>();
        services.AddSingleton<IDashboardAdminService, DashboardAdminService>();
        services.AddSingleton<IRealmKeysAdminService, RealmKeysAdminService>();
        services.AddSingleton<IOrganizationsAdminService, OrganizationsAdminService>();
        services.AddSingleton<IClientLogoutAdminService, ClientLogoutAdminService>();

        services.AddSingleton<IRealmBackupStore, EfRealmBackupStore>();
        if (configuration.GetValue<bool>("IdentityAdmin:EnableBootstrap", true))
            services.AddHostedService<KeycloakBootstrapService>();

        return services;
    }

    /// <summary>
    /// 应用 IdentityAdminDb 迁移。开发期允许直接删库重建（不生成增量迁移）。
    /// </summary>
    public static async Task MigrateIdentityAdminDatabaseAsync(
        this IServiceProvider services,
        CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<IdentityAdminDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Database.MigrateAsync(ct).ConfigureAwait(false);
    }
}
