using MudBlazor.Services;
using TreeGraph.Blazor.Components;
using TreeGraph.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry、HttpClient 弹性
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// ★ EavApiClient：通过 Aspire 服务发现访问 treegrapheavapi
// ServiceDefaults 已为所有 HttpClient 默认启用 ServiceDiscovery + StandardResilience
// ★ 修复 P1-2：移除 HttpClient.Timeout，避免与 StandardResilienceHandler 竞争
// 超时统一由 StandardResilienceHandler 控制
builder.Services
    .AddHttpClient<EavApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
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

// ★ Aspire 默认端点（/health、/alive），供 Dashboard 探测
app.MapDefaultEndpoints();

app.Run();
