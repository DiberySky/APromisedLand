using SemanticSearch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

// 注册 Ollama 客户端（利用 Aspire 服务发现）
builder.AddOllamaApiClient();

// 1. 注册 Elasticsearch 客户端（Aspire 自动从配置读取连接）
builder.AddElasticsearchClient("Elasticsearch");  // 资源名称与 AppHost 一致

// 2. 注册 OllamaSharp 客户端（手动创建，从环境变量获取地址）
builder.Services.AddSingleton<EmbeddingService>();

// 3. 注册业务服务
builder.Services.AddScoped<ElasticsearchService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapDefaultEndpoints();

// ========== 初始化 Elasticsearch 索引和数据 ==========
// 使用后台任务或直接异步执行（确保不阻塞启动）
using (var scope = app.Services.CreateScope())
{
    var esService = scope.ServiceProvider.GetRequiredService<ElasticsearchService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // 重试初始化，应对容器启动延迟
    const int maxRetries = 10;
    int retryCount = 0;
    bool initialized = false;

    do
    {
        try
        {
            logger.LogInformation("正在初始化 Elasticsearch 索引和数据...");
            await esService.EnsureIndexAndDataAsync();
            logger.LogInformation("初始化成功！");
            initialized = true;
        }
        catch (Exception ex)
        {
            retryCount++;
            if (retryCount >= maxRetries)
            {
                logger.LogError(ex, "初始化失败，已达最大重试次数。");
                throw;
            }
            logger.LogWarning(ex, "初始化失败 (尝试 {RetryCount}/{MaxRetries})，等待 5 秒后重试...", retryCount, maxRetries);
            await Task.Delay(5000);
        }
    } while (!initialized);
}

app.Run();