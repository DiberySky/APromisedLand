using MafRagTreeGraph.Components;
using MudBlazor.Services;
using APromisedLand.SharedRazor.Components.TreeGraph;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// TreeApiClient:指向后端 TreeGraphApi (Aspire 编排后端口 5753)
builder.Services.AddHttpClient<TreeApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["TreeGraphApi:BaseUrl"]
        ?? "http://localhost:5753");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
