using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// 1. 添加控制器服务
builder.Services.AddControllers();
builder.Services.AddOpenApi();  // 可选
builder.AddServiceDefaults();
// 2. 准备日志
using var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
});
var logger = loggerFactory.CreateLogger<Program>();

// 3. Foundry Local 配置
var config = new Configuration
{
    AppName = "foundry_local_samples",
    LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
    ModelCacheDir = @"D:\foundry-cache",   // 确保目录存在
    Web = new Configuration.WebService
    {
        Urls = "http://127.0.0.1:5239"     // 内部服务端口
    }
};

// 4. 异步初始化管理器
await FoundryLocalManager.CreateAsync(config, logger);
var mgr = FoundryLocalManager.Instance;

// 5. 执行 EP 下载、模型下载、加载、启动内部服务（一次性完成）
try
{
    // 下载并注册 Execution Providers（必须）
    await mgr.DownloadAndRegisterEpsAsync((epName, percent) =>
    {
        Console.Write($"\r  {epName.PadRight(30)}  {percent,6:F1}%");
    });
    Console.WriteLine();

    // 获取目录并加载模型（使用别名 qwen2.5-7b）
    var catalog = await mgr.GetCatalogAsync();
    var model = await catalog.GetModelAsync("qwen2.5-7b")
                ?? throw new Exception("Model not found");

    // 下载（若已缓存则跳过）
    await model.DownloadAsync(progress =>
    {
        Console.Write($"\rDownloading model: {progress:F2}%");
        if (progress >= 100) Console.WriteLine();
    });

    // 加载到内存
    Console.Write($"Loading model {model.Id}...");
    await model.LoadAsync();
    Console.WriteLine("done.");

    // 启动 Foundry Local 的 HTTP 服务
    Console.Write($"Starting web service on {config.Web.Urls}...");
    await mgr.StartWebServiceAsync();
    Console.WriteLine("done.");
}
catch (Exception ex)
{
    logger.LogError(ex, "初始化 Foundry Local 失败");
    // 可根据需要决定是否继续启动
}

// 6. 注册管理器单例
builder.Services.AddSingleton(mgr);

// 7. 构建应用
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();   // 映射控制器路由

// 8. 运行主应用（监听在 Program.cs 中配置的端口，如 http://localhost:5183）
app.Run();