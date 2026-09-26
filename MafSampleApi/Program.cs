using MafSampleApi.Models;      // ← AgentOptions 在这（见下方注意）
using MafSampleApi.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. 配置绑定（关键！少了这段 IOptions<AgentOptions> 会解析失败）
builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateOnStart();

// ─── 2. Ollama 端点
var ollamaEndpoint =
    Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
    ?? builder.Configuration["Ollama:Endpoint"]
    ?? "http://localhost:11434";
var ollamaUri = new Uri(ollamaEndpoint);
builder.Services.AddSingleton(ollamaUri);

// ─── 3. OllamaApiClient（单例）
builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    return new OllamaApiClient(ollamaUri, opts.ChatModel);
});

// ─── 4. 暴露为 IChatClient（AgentFactory 依赖它）
builder.Services.AddSingleton<IChatClient>(sp =>
    sp.GetRequiredService<OllamaApiClient>());

// ─── 5. MAF 服务（★ 必须用接口映射写法）
builder.Services.AddSingleton<IAgentFactory, AgentFactory>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddHostedService<OllamaWarmupService>();

// ─── 6. ASP.NET Core
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