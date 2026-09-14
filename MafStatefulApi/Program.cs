using MafStatefulApi.Agents;
using MafStatefulApi.State;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Caching.StackExchangeRedis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

// 资源名统一为 "Redis"，与 AppHost 中 builder.AddRedis("Redis") 一致
builder.AddRedisDistributedCache("Redis");

// 清空 InstanceName，保证 IDistributedCache 的 key 前缀与
// RedisAgentSessionStore.ListSessionsAsync 的 SCAN pattern "maf:sessions:*" 一致
builder.Services.PostConfigure<RedisCacheOptions>(options =>
{
    options.InstanceName = string.Empty;
});

// 注册非 keyed IConnectionMultiplexer，
// 与 AgentRunner 的构造函数参数 IConnectionMultiplexer redis 匹配
builder.AddRedisClient("Redis");

builder.Services.AddSingleton<IAgentSessionStore, RedisAgentSessionStore>();

builder.Services.ConfigureHttpClientDefaults(httpBuilder =>
{
    httpBuilder.AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(300);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(7);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(600);
    });
});

builder.AddOllamaApiClient("chat-model").AddChatClient();

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

builder.Services.AddScoped<AgentRunner>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.Run();

#pragma warning disable CA1812
public partial class Program;
#pragma warning restore CA1812