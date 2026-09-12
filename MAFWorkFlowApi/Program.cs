using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#pragma warning disable EXTEXP0001

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ⭐ 全局配置：移除 Polly 弹性管道 + 设置长超时
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.RemoveAllResilienceHandlers();
    http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(5));
});

// Ollama 客户端注册（不再链式 RemoveAllResilienceHandlers）
builder.AddOllamaApiClient("chat-model")
    .AddKeyedChatClient("chat-model");
builder.AddOllamaApiClient("embedding")
    .AddKeyedEmbeddingGenerator("embedding");

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services
    .AddOptions<OllamaAgentOptions>()
    .Bind(builder.Configuration.GetSection(OllamaAgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<MafAgentService>();

builder.Services.AddHealthChecks()
    .AddCheck<OllamaModelReadyHealthCheck>(
        name: "ollama-model-ready",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseRouting();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

#pragma warning restore EXTEXP0001