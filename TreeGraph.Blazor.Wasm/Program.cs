using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using TreeGraph.Blazor.Components;
using TreeGraph.Blazor.Configuration;
using TreeGraph.Blazor.Infrastructure.Admin;
using TreeGraph.Blazor.Infrastructure.Identity;
using TreeGraph.Blazor.Shared.FileStorageSky;
using TreeGraph.Blazor.Shared.FileStorageSky.Services;
using TreeGraph.Blazor.Shared.LineSky.Services;
using TreeGraph.Blazor.Shared.MafWfSky.Extensions;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.Responsive.Extensions;
using TreeGraph.Blazor.Shared.SpaceSky.Services;
using TreeGraph.Blazor.Shared.TreeEavSky.Extensions;
using TreeGraph.Blazor.Shared.TreeEavSky.Services;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;
using TreeGraph.Shared.TreeEavSky.Entities;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

using var configClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
var runtimeConfig = await configClient.GetFromJsonAsync<WasmRuntimeConfig>("config.json")
    ?? throw new InvalidOperationException("无法加载运行时 config.json。");
runtimeConfig.Validate();
builder.Services.AddSingleton(runtimeConfig);

builder.Services.AddHttpClient("oidc");
builder.Services.AddSingleton<WasmOidcSession>();
builder.Services.AddScoped<AuthenticationStateProvider, WasmAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore(options =>
    options.AddPolicy(IdentityConstants.AdminOnlyPolicy, policy =>
        policy.RequireRole(IdentityRoles.Superuser, IdentityRoles.Admin, "app_admin")));
builder.Services.AddTransient<BearerAuthenticationHandler>();

builder.Services.AddMudServices();
builder.Services.AddTreeGraphPlatform();
builder.Services.AddResponsive();
builder.Services.AddTreeEavSky(
    configureClient: client => client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.TreeGraphApi),
    configureClientBuilder: http => http.AddHttpMessageHandler<BearerAuthenticationHandler>());
builder.Services.AddScoped<ITreeClientService<UnitTree>, UnitTreeClientService>();
builder.Services.AddScoped<ITreeClientService<CategoryTree>, CategoryTreeClientService>();
builder.Services.AddScoped<ITreeClientService<KnowledgeTree>, KnowledgeTreeClientService>();
builder.Services.AddScoped<ITreeClientService<SpaceTree>, SpaceTreeClientService>();
builder.Services.AddScoped<ITreeClientService<EavTree>, EavTreeClientService>();
builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();
builder.Services.AddScoped<EntityTypeDisplayService>();

builder.Services.AddHttpClient("treegrapheavapi", client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.TreeGraphApi))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();
builder.Services.AddHttpClient("treegraphsyncservices", client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.SyncServices))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();
builder.Services.AddTransient(sp => new EavApiClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("treegrapheavapi"),
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("treegraphsyncservices"),
    sp.GetRequiredService<ILogger<EavApiClient>>()));

builder.Services.AddHttpClient<LineApiClient>(client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.TreeGraphApi))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();
builder.Services.AddHttpClient<SpaceApiClient>(client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.TreeGraphApi))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();
builder.Services.AddHttpClient<TreeGraph.Blazor.Shared.SpaceNodeSky.Services.SpaceNodeApiClient>(client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.TreeGraphApi))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();
builder.Services.AddHttpClient<TreeGraph.Blazor.Shared.EdgeSky.Services.EdgeApiClient>(client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.TreeGraphApi))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();
builder.Services.AddHttpClient<LineManageApiClient>(client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.TreeGraphApi))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();
builder.Services.AddHttpClient<LineNodeManageApiClient>(client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.TreeGraphApi))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();

builder.Services.AddSingleton(new FileStorageSkyOptions { UploadsPath = "Uploads", FilesPath = "Files" });
builder.Services.AddHttpClient<IFileStorageClient, FileStorageApiClient>(client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.FileStorageApi))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();
builder.Services.AddMafWfSky(
    runtimeConfig.Apis.MafWfApi,
    configureClientBuilder: http => http.AddHttpMessageHandler<BearerAuthenticationHandler>());

builder.Services.AddHttpClient<IdentityAdminApiClient>(client =>
        client.BaseAddress = WasmRuntimeConfig.ApiBaseAddress(runtimeConfig.Apis.IdentityAdminApi))
    .AddHttpMessageHandler<BearerAuthenticationHandler>();
builder.Services.AddScoped<IAdminActorAccessor, WasmAdminActorAccessor>();
builder.Services.AddScoped<StepUpGuard>();
builder.Services.AddScoped<IUsersAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IDashboardAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IGroupsAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IRolesAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IClientsAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IClientScopesAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IIdentityProvidersAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IFederationAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IAuthFlowsAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IRealmSettingsAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IRealmKeysAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IOrganizationsAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IEventsAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<ISessionsAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IClientLogoutAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IExportAdminService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());
builder.Services.AddScoped<IAdminAuditQueryService>(sp => sp.GetRequiredService<IdentityAdminApiClient>());

await builder.Build().RunAsync();
