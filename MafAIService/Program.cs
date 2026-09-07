using MafAIService.Agents;
using MafAIService.State;
using Microsoft.Agents.AI.Hosting;

var builder = WebApplication.CreateBuilder(args);


builder.AddServiceDefaults();
// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 可选：配置 JSON 序列化设置，与先前保持一致
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Redis 分布式缓存
builder.AddRedisDistributedCache("Redis");
builder.AddRedisClient("Redis");
builder.Services.AddSingleton<IAgentSessionStore, RedisAgentSessionStore>();

// Ollama 与 AI Agent
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

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();