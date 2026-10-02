using Microsoft.Extensions.Http.Resilience;
using MudBlazor.Services;
using Polly;
using TreeGraph.Blazor.Components;
using TreeGraph.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry、HttpClient 弹性
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// ★ 前端字段校验器（单例，无状态）
builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();

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
        // ★ 关键：禁止重试。管理台操作由用户主动发起，失败时点击重试即可，
        // 比自动重试导致数据损坏更安全。
        options.Retry.MaxRetryAttempts = 0;

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
