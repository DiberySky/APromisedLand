using MudBlazor.Services;
using TreeGraph.Blazor.Components;
using TreeGraph.Blazor.Infrastructure;
using TreeGraph.Blazor.Shared.NodeEav.Services;
using TreeGraph.Blazor.Services.DemoTree;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Extensions;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Models;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Services;
using TreeGraph.Blazor.Shared.Trees.StringTree.Extensions;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.StringTreeSky;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry、HttpClient 弹性
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// ★ 平台上下文：JS 视口检测（600/960 断点），Scoped。Hybrid 可覆盖注册。
builder.Services.AddTreeGraphPlatform();

// ★ TreeSky 树组件库（BlazorService/MessageService/TreeNodeDialogService/导航/泛型树 API 客户端 + MudExtensions）
builder.Services.AddTreeSky();

// ★ TreeSky 演示：StringTreeNode（string 名称节点），数据存于 TreeGraph.Api 的
//   string_tree_nodes 表（Postgres）。读写都经 DiberyTreeApiClient<StringTreeNode>
//   → /StringTreeNode/* 端点（TreeControllerBase<StringTreeNode> + EfTreeService）。
builder.Services.AddScoped<ITreeClientService<StringTreeNode>, StringTreeClientService>();
builder.Services.AddHttpClient("TreeSky", client =>
    {
        // Aspire 服务发现：与 EavApiClient 同一后端
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    // 写操作（POST/PUT/DELETE/move）不幂等，禁止自动重试。
    // 具体参数见 NonIdempotentResilience，与 EavApiClient 共用同一套策略。
    .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

// ★ StringTreeSky（T=string 树）：Noop Handler 用 TryAdd 注册，
//   必须在宿主自定义 Handler 之前，下面的 AddScoped 才能覆盖默认值。
builder.Services.AddStringTreeSky();

// ★ StringTreeSky（T=string 树）：HTTP 版数据源 + 操作 Handler，
//   桥接 DiberyTreeApiClient<StringTreeNode> → /StringTreeNode/* 端点。
//   Noop Handler 由 AddStringTreeSky() 内部 TryAdd 注册，在此之前已执行，
//   下面的 AddScoped 覆盖默认 Noop。
builder.Services.AddScoped<IStringTreeDataSource, ApiStringTreeDataSource>();
builder.Services.AddScoped<IStringTreeActionHandler, ApiStringTreeActionHandler>();

// ★ StringTreeSky（解耦版）：独立契约层 + 非泛型 HTTP 客户端，端点 api/string-tree/*。
//   与 EavApiClient 同一 Aspire 服务发现与弹性策略。
//   注：不采用文档片段的 AddHttpClient + AddStringTreeSky 双注册——
//   AddStringTreeSky 内部 AddScoped<IStringTreeClient, StringTreeApiClient> 无
//   HttpClient 可注入，会覆盖类型化客户端注册导致 BaseAddress 丢失。
builder.Services.AddSingleton<StringTreeSkyOptions>(_ => new StringTreeSkyOptions
{
    BasePath = "api/string-tree",
    DefaultExpandLevel = 1,
});
builder.Services
    .AddHttpClient<IStringTreeClient, StringTreeApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

// ★ StringTreeSky 内存演示（备用，未注册）：
//   InMemoryStringTreeStore / InMemoryStringTreeDataSource / InMemoryStringTreeActionHandler
//   保留在 Services/DemoTree/ 下供离线演示或测试参考，不注入到容器。

// ★ 前端字段校验器（单例，无状态）
builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();
builder.Services.AddScoped<EntityTypeDisplayService>();

// ★ EavApiClient：通过 Aspire 服务发现访问 treegrapheavapi。
//   弹性策略（禁止重试、超时、熔断器采样窗口）统一由 NonIdempotentResilience 提供，
//   与 TreeSky 客户端保持一致，避免同一后端操作在不同客户端上行为不同。
builder.Services
    .AddHttpClient<EavApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
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

