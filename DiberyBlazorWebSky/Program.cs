using DiberyBlazorWebSky.Components;
using DiberyBlazorWebSky.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Aspire 默认
// ---------------------------------------------------------------------------
builder.AddServiceDefaults();

// ★ 全局移除 Aspire 默认弹性管道，避免 AttemptTimeout=10s 截断 Ollama 长推理
#pragma warning disable EXTEXP0001
builder.Services.ConfigureHttpClientDefaults(http => { http.RemoveAllResilienceHandlers(); });
#pragma warning restore EXTEXP0001

// ---------------------------------------------------------------------------
// 2. Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// 3. API 客户端注册
//    - 统一基地址：https+http://MAFWorkFlowApi
//    - 统一超时：10 分钟，覆盖 Ollama 冷启动与长生成
// ---------------------------------------------------------------------------
builder.Services.AddHttpClient<ChatApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://MAFWorkFlowApi");
    client.Timeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddHttpClient<WorkflowApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://MAFWorkFlowApi");
    client.Timeout = TimeSpan.FromMinutes(10);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();