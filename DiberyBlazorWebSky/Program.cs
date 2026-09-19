using DiberyBlazorWebSky.Components;
using DiberyBlazorWebSky.Endpoints;
using DiberyBlazorWebSky.Services;
using LiteGraph;
using LiteGraph.Sdk;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();

// ── MAFWorkFlowApi ──
builder.Services.AddHttpClient<ChatApiClient>(client =>
{
    client.BaseAddress = new("https+http://MAFWorkFlowApi");
});

builder.Services.AddHttpClient<WorkflowApiClient>(client =>
{
    client.BaseAddress = new("https+http://MAFWorkFlowApi");
});

// ── MAFWorkFlowApi：Graph 页面 ──
builder.Services.AddHttpClient<GraphApiClient>(client =>
{
    client.BaseAddress = new("https+http://MAFWorkFlowApi");
    client.Timeout     = TimeSpan.FromMinutes(5);
});

builder.Services.AddSingleton<LiteGraphSdk>(sp =>
{
    // LiteGraph REST Server 运行在 8701 端口
    // "default" 是默认租户标识
    return new LiteGraphSdk("http://localhost:8701", "default");
});

builder.Services.AddScoped<GraphImportExportService>();

builder.Services.AddScoped<GraphDynamicContextService>();

// ── FileStorageApi ──
// ★ 修复：
//   1) RemoveAllResilienceHandlers() 移除 AddServiceDefaults() 由
//      ConfigureHttpClientDefaults 注入的默认 Standard Resilience Handler
//      （默认 AttemptTimeout = 10s，对大文件上传远远不够）。
//   2) 挂自定义 Handler：禁用非幂等重试、放宽超时。
//   3) AttemptTimeout 覆盖单次请求最长耗时（含 S3 multipart 合并）。
//   4) CircuitBreaker.SamplingDuration ≥ 2 × AttemptTimeout（Polly 硬性校验）。
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers 是实验性 API
builder.Services.AddHttpClient<FileStorageApiClient>(client =>
    {
        client.BaseAddress = new("https+http://FileStorageApi");
        client.Timeout     = TimeSpan.FromMinutes(60);
    })
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.DisableForUnsafeHttpMethods();

        options.AttemptTimeout.Timeout          = TimeSpan.FromMinutes(15);
        options.TotalRequestTimeout.Timeout     = TimeSpan.FromMinutes(60);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(30);
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();
app.MapDownloadEndpoints();   // ★ 新增

app.Run();