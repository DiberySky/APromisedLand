using MudBlazor.Services;
using TreeGraph.Blazor.Components;
using TreeGraph.Blazor.Infrastructure;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.TreeEavSky.Extensions;
using TreeGraph.Blazor.Shared.TreeEavSky.Models;
using TreeGraph.Blazor.Shared.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.StringTreeSky;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;
using TreeGraph.Blazor.Shared.FileStorageSky;
using TreeGraph.Blazor.Shared.FileStorageSky.Services;
using TreeGraph.Blazor.Shared.Responsive.Extensions;
#if DEBUG
using TreeGraph.Blazor.Shared.Common.Dev;
#endif

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry、HttpClient 弹性
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

#if DEBUG
// 弹窗源文件定位（DEBUG 专用）：装饰 IDialogService，PageDialogSky 标题栏 DevDialogSourceButton 据此反查 .razor 文件
builder.Services.AddDialogSourceTracker();
#endif

// ★ 平台上下文：JS 视口检测（600/960 断点），Scoped。Hybrid 可覆盖注册。
builder.Services.AddTreeGraphPlatform();

// ★ TreeEavSky 树组件库（BlazorService/MessageService/TreeNodeDialogService/导航/泛型树 API 客户端 + MudExtensions）
//   named HttpClient 指向 treegrapheavapi（Aspire 服务发现），写操作统一 NonIdempotentResilience。
builder.Services.AddTreeEavSky(
    configureClient: client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    },
    configureClientBuilder: httpBuilder =>
        httpBuilder.AddStandardResilienceHandler(NonIdempotentResilience.Configure));

// ★ 泛型树组件读操作适配器（ITreeClientService<T>）：包装 TreeApiClient<T>，
//   添加排序逻辑与 UI 属性，供 TreeEavSky.razor / TreeDialogPageSky.razor / TreeSelectDialogSky.razor 注入。
builder.Services.AddScoped<ITreeClientService<UnitTree>, UnitTreeClientService>();
builder.Services.AddScoped<ITreeClientService<CategoryTree>, CategoryTreeClientService>();
builder.Services.AddScoped<ITreeClientService<StringTreeNode>, StringTreeNodeClientService>();

// ★ StringTreeSky（解耦版）：独立契约层 + 非泛型 HTTP 客户端，端点 api/string-tree/*。
//   与 EavApiClient 同一 Aspire 服务发现与弹性策略。
//   注：不采用文档片段的 AddHttpClient + AddStringTreeSky 双注册——
//   AddStringTreeSky 内部 AddScoped<IStringTreeClient, StringTreeApiClient> 无
//   HttpClient 可注入，会覆盖类型化客户端注册导致 BaseAddress 丢失。
builder.Services.AddSingleton<StringTreeSkyOptions>(_ => new StringTreeSkyOptions
{
    BasePath = "api/string-tree",
    DefaultExpandLevel = 1,
    AllowFilter = true,
});
builder.Services
    .AddHttpClient<IStringTreeClient, StringTreeApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

// ★ 空间客户端（api/string-tree/spaces）：与 StringTreeApiClient 同地址、同弹性策略。
builder.Services
    .AddHttpClient<ISpaceClient, SpaceApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

// ★ 节点属性摘要 + Schema 缓存（增强项，默认旁路：SummaryAttributeNames 为空即零请求）。
//   解耦版 AddStringTreeSky 扩展内含同注册；宿主手工注册 IStringTreeClient，
//   故此处补注册，避免组件 [Inject] 无法解析。
builder.Services.AddScoped<NodeSchemaCache>();
builder.Services.AddScoped<NodePropertySummaryService>();

// ★ 空间-EntityType 解析 + 属性统计（空间管理徽章列 / 元数据按空间分组页使用）。
//   宿主无参 AddStringTreeSky() 绑定的是 Trees\StringTree 旧扩展，未含解耦版
//   StringTreeSky\Extensions 中的这两项注册，故与上方相同方式补注册。
builder.Services.AddScoped<SpaceEntityTypeResolver>();
builder.Services.AddScoped<SpaceAttributeStatsService>();

// ★ 通用响应式模块（ResponsiveView / ResponsiveSplit 的 ViewportService）。
builder.Services.AddResponsive();

// ★ 前端字段校验器（单例，无状态）
builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();
builder.Services.AddScoped<EntityTypeDisplayService>();

// ★ EavApiClient：通过 Aspire 服务发现访问 treegrapheavapi。
//   弹性策略（禁止重试、超时、熔断器采样窗口）统一由 NonIdempotentResilience 提供，
//   与 TreeEavSky 客户端保持一致，避免同一后端操作在不同客户端上行为不同。
builder.Services
    .AddHttpClient<EavApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

// ★ FileStorageSky 客户端（UploadsController + FilesController）：独立微服务 treegraphfilestorageapi。
//   Aspire 服务发现名 "treegraphfilestorageapi"（见 AppHost/Extensions/TreeGraphFileStorageApiExtension.cs），
//   全小写,与 treegrapheavapi 风格一致;TreeGraph.BlazorExtension 已通过
//   WithReference(TreeGraphFileStorageApi) 注入服务发现。
builder.Services.AddSingleton<FileStorageSkyOptions>(_ => new FileStorageSkyOptions
{
    UploadsPath = "Uploads",
    FilesPath = "Files",
});
builder.Services
    .AddHttpClient<IFileStorageClient, FileStorageApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegraphfilestorageapi");
    })
    .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();

// ★ 供 WebApplicationFactory<Program> 引用（启动级 smoke 测试需要）
public partial class Program { }

