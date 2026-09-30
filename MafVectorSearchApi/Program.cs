using MafVectorSearchApi.Models;
using MafVectorSearchApi.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. 配置绑定 ───
builder.Services
    .AddOptions<EmbeddingOptions>()
    .Bind(builder.Configuration.GetSection(EmbeddingOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<RerankerOptions>()
    .Bind(builder.Configuration.GetSection(RerankerOptions.SectionName))
    .ValidateOnStart();

// ═══════════════════════════════════════════════════════════
// 2. vLLM Embedding 客户端
// ═══════════════════════════════════════════════════════════
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    var endpoint = Environment.GetEnvironmentVariable("VLLM_EMBEDDING_HTTP")
                   ?? opts.Endpoint;

    logger.LogInformation(
        "IEmbeddingGenerator → {Endpoint}, 模型: {Model}",
        endpoint, opts.Model);

    var openAiOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint.TrimEnd('/') + "/v1"),
        NetworkTimeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
    };

    return new OpenAIClient(
            new ApiKeyCredential(opts.ApiKey),
            openAiOptions)
        .GetEmbeddingClient(opts.Model)
        .AsIEmbeddingGenerator();
});

// ═══════════════════════════════════════════════════════════
// 3. Reranker 客户端
// ═══════════════════════════════════════════════════════════
builder.Services.AddHttpClient<IRerankerClient, RerankerClient>("reranker",
    (sp, http) =>
    {
        var opts = sp.GetRequiredService<IOptions<RerankerOptions>>().Value;
        var endpoint = Environment.GetEnvironmentVariable("Reranker__Endpoint")
                       ?? opts.Endpoint;
        http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
        http.Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        sp.GetRequiredService<ILogger<Program>>()
            .LogInformation("Reranker client → {Endpoint}", endpoint);
    });

// ─── 4. 向量搜索服务 ───
builder.Services.AddSingleton<RagService>();

// ─── 5. ASP.NET Core ───
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

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
