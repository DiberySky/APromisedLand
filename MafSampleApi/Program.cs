using MafSampleApi.Models;
using MafSampleApi.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. 配置绑定
builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateOnStart();

// ─── 2. vLLM / OpenAI 兼容端点
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    var endpoint = Environment.GetEnvironmentVariable("VLLM_HTTP")
                   ?? opts.Endpoint
                   ?? "http://localhost:8000";

    logger.LogInformation("IChatClient 指向: {Endpoint}, 模型: {Model}", endpoint, opts.ChatModel);

    var openAiOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint.TrimEnd('/') + "/v1")
    };

    return new OpenAIClient(
            new ApiKeyCredential("EMPTY"),
            openAiOptions)
        .GetChatClient(opts.ChatModel)
        .AsIChatClient();
});

// ─── 3. MAF 服务
builder.Services.AddSingleton<IAgentFactory, AgentFactory>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
// ★ 删掉 OllamaWarmupService —— 那是 Ollama 专用的
// builder.Services.AddHostedService<OllamaWarmupService>();

// ─── 4. ASP.NET Core
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Run();