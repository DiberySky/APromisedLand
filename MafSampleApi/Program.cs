using MafSampleApi.Models;
using MafSampleApi.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;
using MafSampleApi.Services.Tools;

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
// 3. vLLM Embedding 客户端
// ═══════════════════════════════════════════════════════════
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    var endpoint = Environment.GetEnvironmentVariable("VLLM_EMBEDDING_HTTP")
                   ?? opts.EmbeddingEndpoint
                   ?? "http://localhost:8001";

    logger.LogInformation(
        "IEmbeddingGenerator → {Endpoint}, 模型: {Model}",
        endpoint, opts.EmbeddingModel);

    var openAiOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint.TrimEnd('/') + "/v1"),
        NetworkTimeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
    };

    return new OpenAIClient(
            new ApiKeyCredential(opts.ApiKey),
            openAiOptions)
        .GetEmbeddingClient(opts.EmbeddingModel)
        .AsIEmbeddingGenerator();
});

// ═══════════════════════════════════════════════════════════
// 4. vLLM 探活专用 HttpClient（★ 就是缺这一块）
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
// 5. Reranker 客户端
// ═══════════════════════════════════════════════════════════
builder.Services
    .AddOptions<RerankerOptions>()
    .Bind(builder.Configuration.GetSection(RerankerOptions.SectionName));

builder.Services.AddHttpClient<IRerankerClient, RerankerClient>("reranker",
    (sp, http) =>
    {
        var opts = sp.GetRequiredService<IOptions<RerankerOptions>>().Value;
        var endpoint = Environment.GetEnvironmentVariable("Reranker__Endpoint")
                       ?? opts.Endpoint
                       ?? "http://localhost:5919";
        http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
        http.Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        sp.GetRequiredService<ILogger<Program>>()
            .LogInformation("Reranker client → {Endpoint}", endpoint);
    });

// ─── 6. MAF 服务 ────────────────────────────────────────────
builder.Services.AddSingleton<IAgentFactory, AgentFactory>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddHostedService<VllmWarmupService>();
builder.Services.AddSingleton<IInstructionTemplateStore, InMemoryInstructionTemplateStore>();
builder.Services.AddSingleton<AgentSessionStore>();
builder.Services.AddHostedService<SessionCleanupService>();

// ─── 6.1 工具注册 ─────────────────────────────
builder.Services.AddSingleton<TimeTools>();
builder.Services.AddSingleton<MathTools>();
builder.Services.AddSingleton<TemplateTools>();
builder.Services.AddSingleton<KnowledgeTools>();
builder.Services.AddSingleton<IToolRegistry, DefaultToolRegistry>();

// ─── 7. ASP.NET Core ───────────────────────────────────────
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