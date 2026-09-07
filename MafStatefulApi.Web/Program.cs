using MafStatefulApi.Web.Components;
using MafStatefulApi.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


// 移除所有默认的弹性处理器（包括 AddServiceDefaults 添加的）
builder.Services.ConfigureHttpClientDefaults(httpBuilder =>
{
    httpBuilder.AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(300);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(7);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(600);
    });
});

// Add HttpClient for API communication with service discovery
builder.Services.AddHttpClient("api", (serviceProvider, client) =>
{
    // Use Aspire service discovery to resolve the API endpoint
    client.BaseAddress = new Uri("http://api");
    client.Timeout = TimeSpan.FromMinutes(7);
});

// Register ChatApiService
builder.Services.AddScoped<ChatApiService>();

var app = builder.Build();

// Map Aspire default endpoints (health checks)
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
