using MafStatefulApi.Agents;
using MafStatefulApi.State;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add OpenAPI support
builder.Services.AddOpenApi();

// Configure Redis distributed cache for session persistence
builder.AddRedisDistributedCache("cache");

// Also register IConnectionMultiplexer for advanced Redis operations
builder.AddRedisClient("cache");

builder.Services.AddSingleton<IAgentSessionStore, RedisAgentSessionStore>();
builder.Services.AddLogging(logging => logging.AddConsole());
Console.WriteLine("Using Redis for session storage");

builder.Services.ConfigureHttpClientDefaults(httpBuilder =>
{
    httpBuilder.AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(300);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(7);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(600);
    });
});

// Configure Microsoft Agent Framework with Ollama
builder.AddOllamaApiClient("chat-model").AddChatClient();

Console.WriteLine("Using Ollama for AI model");

builder.AddAIAgent(
    name: "AssistantAgent",
    instructions: @"你是一个友好且乐于助人的人工智能助手。
        指导方针：
            -回答要简洁明了
            -记住对话中以前消息的上下文
            -当被问及之前的消息时，请参考对话历史记录
            -使用易于理解的简单语言
            -如果你知道这个名字是用户，一定要在你的回复中使用它
            -如果你不知道什么，老实说
        ");

// Register AgentRunner
builder.Services.AddScoped<AgentRunner>();

// ===== 添加 API Controller 支持 =====
builder.Services.AddControllers();

var app = builder.Build();

// Map Aspire default endpoints (health checks)
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ===== 使用 API Controller 路由 =====
app.MapControllers();

app.Run();

// Make Program accessible for testing
public partial class Program
{
}