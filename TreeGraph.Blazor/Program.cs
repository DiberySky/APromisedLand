using Microsoft.Extensions.Http.Resilience;
using MudBlazor.Services;
using Polly;
using TreeGraph.Blazor.Components;
using TreeGraph.Blazor.Services;
using TreeGraph.Blazor.Services.DemoTree;
using TreeGraph.TreeSky;
using TreeGraph.TreeSky.Models;
using TreeGraph.TreeSky.Services;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry、HttpClient 弹性
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

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
    // 写操作（POST/PUT/DELETE/move）不幂等，禁止自动重试
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
    });

// ★ 前端字段校验器（单例，无状态）
builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();
builder.Services.AddScoped<EntityTypeDisplayService>();

// ★ EavApiClient：通过 Aspire 服务发现访问 treegrapheavapi
// 弹性策略显式配置：
//   - 关闭自动重试：元数据 PUT/POST 不幂等，自动重试会引发数据损坏
//     （例如 recalculate-factor 被重放 → 值被平方调整）
//   - 放宽超时：单次重算可能耗时几秒
//   - 保留熔断器默认参数（保护后端）
builder.Services
    .AddHttpClient<EavApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    .AddStandardResilienceHandler(options =>
    {
        // ★ 禁止重试的正确写法：
        //   MaxRetryAttempts 校验约束为 1–int.MaxValue（不接受 0），
        //   因此置 1 通过校验，再用 ShouldHandle 恒 false 让重试永不触发。
        //   管理台 PUT/POST 不幂等（如 recalculate-factor 重放会导致数据损坏）。
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);

        // ★ 熔断器采样窗口必须 ≥ 2 × AttemptTimeout（30s → 至少 60s）
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);

        // 超时设置
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    });

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

