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

// ═══════════════════════════════════════════════════════════
// 2. vLLM Chat 客户端 + think 剥离装饰器
// ═══════════════════════════════════════════════════════════
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var opts   = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    var endpoint = Environment.GetEnvironmentVariable("VLLM_HTTP")
                   ?? opts.Endpoint
                   ?? "http://localhost:8000";

    logger.LogInformation(
        "IChatClient → {Endpoint}, 模型: {Model}", endpoint, opts.ChatModel);

    var openAiOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint.TrimEnd('/') + "/v1")
    };

    IChatClient inner = new OpenAIClient(
            new ApiKeyCredential(opts.ApiKey),
            openAiOptions)
        .GetChatClient(opts.ChatModel)
        .AsIChatClient();

    return new ThinkStrippingChatClient(
        inner,
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
        Endpoint = new Uri(endpoint.TrimEnd('/') + "/v1")
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

// ─── 5. MAF 服务 ────────────────────────────────────────────
builder.Services.AddSingleton<IAgentFactory, AgentFactory>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddHostedService<VllmWarmupService>();

// ─── 6. ASP.NET Core ───────────────────────────────────────
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