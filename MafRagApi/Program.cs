using MafRagApi.Models;
using MafRagApi.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;
using MafRagApi.Services.Tools;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. 配置绑定
builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateOnStart();

// ═══════════════════════════════════════════════════════════
// 2. vLLM Chat 客户端 + think 剥离装饰器
// ═══════════════════════════════════════════════════════════
// 注册 vLLM HttpClient（必须在 Build 前注册）
builder.Services.AddHttpClient("vllm-chat", (sp, c) =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    var endpoint = Environment.GetEnvironmentVariable("VLLM_HTTP")
                   ?? opts.Endpoint ?? "http://localhost:8000";
    c.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/v1/");
    c.Timeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    var endpoint = Environment.GetEnvironmentVariable("VLLM_HTTP")
                   ?? opts.Endpoint ?? "http://localhost:8000";

    logger.LogInformation(
        "IChatClient → {Endpoint}, 模型: {Model}", endpoint, opts.ChatModel);

    // ★ 自定义 VllmChatClient（直接调用 vLLM，注入 enable_thinking=false）
    return new ThinkStrippingChatClient(
        new VllmChatClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("vllm-chat"),
            opts.ChatModel,
            opts.ApiKey,
            sp.GetRequiredService<ILogger<VllmChatClient>>()),
        sp.GetRequiredService<ILogger<ThinkStrippingChatClient>>());
});

// ═══════════════════════════════════════════════════════════
// 3. vLLM 探活专用 HttpClient
// ═══════════════════════════════════════════════════════════
builder.Services.AddHttpClient("vllm", (sp, http) =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;

    var endpoint = Environment.GetEnvironmentVariable("VLLM_HTTP")
                   ?? opts.Endpoint
                   ?? "http://localhost:8000";

    http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(5);
});

// ═══════════════════════════════════════════════════════════
// 4. 向量搜索服务客户端（远程调用 MafVectorSearchApi）
// ═══════════════════════════════════════════════════════════
builder.Services.AddHttpClient<VectorSearchClient>((sp, http) =>
{
    var baseUrl = Environment.GetEnvironmentVariable("VECTOR_SEARCH__BASEURL")
                  ?? builder.Configuration["VectorSearch:BaseUrl"]
                  ?? "http://localhost:5741";
    http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(120);
    sp.GetRequiredService<ILogger<Program>>()
        .LogInformation("VectorSearch client → {BaseUrl}", baseUrl);
});

// ─── 5. MAF 服务 ────────────────────────────────────────────
builder.Services.AddSingleton<IAgentFactory, AgentFactory>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddHostedService<VllmWarmupService>();
builder.Services.AddSingleton<IInstructionTemplateStore, InMemoryInstructionTemplateStore>();
builder.Services.AddSingleton<AgentSessionStore>();
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddSingleton<RagChatOrchestrator>();
builder.Services.AddSingleton<WorkflowService>();

// ─── 5.1 工具注册 ─────────────────────────────
builder.Services.AddSingleton<TimeTools>();
builder.Services.AddSingleton<MathTools>();
builder.Services.AddSingleton<TemplateTools>();
builder.Services.AddSingleton<KnowledgeTools>();
builder.Services.AddSingleton<IToolRegistry, DefaultToolRegistry>();

// ─── 6. ASP.NET Core ───────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseStaticFiles();   // ← 新增
app.UseCors();          // 如果已经加了 CORS 就留着

app.UseAuthorization();
app.MapControllers();

app.Run();
