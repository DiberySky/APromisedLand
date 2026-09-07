// using Microsoft.AI.Foundry.Local;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
//
// namespace APromisedLand.Api.Configuration;
//
// public class ApiConfigs
// {
//     public async Task FoundryLocalConfig(IServiceCollection services, string alias = "phi-4-mini")
//     {
//         // var alias = "phi-4-mini"; //deepseek-r1-7b phi-4-mini qwen3-8b  qwen2.5-7b
//         
//         // 2. 准备日志配置
//         using var loggerFactory = LoggerFactory.Create(logging =>
//         {
//             logging.AddConsole();
//         });
//         var logger = loggerFactory.CreateLogger<ApiConfigs>();
//
// // 3. Foundry Local 配置
//         var config = new Microsoft.AI.Foundry.Local.Configuration
//         {
//             AppName = "foundry_local_samples",
//             LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
//             ModelCacheDir = @"D:\foundry-cache",   // 确保目录存在
//             Web = new Microsoft.AI.Foundry.Local.Configuration.WebService
//             {
//                 Urls = "http://127.0.0.1:5239"     // 内部服务端口
//             }
//         };
//
// // 4. 异步初始化管理器
//         await FoundryLocalManager.CreateAsync(config, logger);
//         var mgr = FoundryLocalManager.Instance;
//
// // 5. 执行 EP 下载、模型下载、加载、启动内部服务（一次性完成）
//         try
//         {
//             // 下载并注册 Execution Providers（必须）
//             await mgr.DownloadAndRegisterEpsAsync((epName, percent) =>
//             {
//                 Console.Write($"\r  {epName,-30}  {percent,6:F1}%");
//             });
//             Console.WriteLine();
//
//             // 获取目录并加载模型（使用别名 qwen2.5-7b）
//             var catalog = await mgr.GetCatalogAsync();
//             var model = await catalog.GetModelAsync(alias)
//                         ?? throw new Exception("Model not found");
//
//             // 下载（若已缓存则跳过）
//             await model.DownloadAsync(progress =>
//             {
//                 Console.Write($"\rDownloading model: {progress:F2}%");
//                 if (progress >= 100) Console.WriteLine();
//             });
//
//             // 加载到内存
//             Console.Write($"Loading model {model.Id}...");
//             await model.LoadAsync();
//             Console.WriteLine("done.");
//
//             // 启动 Foundry Local 的 HTTP 服务
//             Console.Write($"Starting web service on {config.Web.Urls}...");
//             await mgr.StartWebServiceAsync();
//             Console.WriteLine("done.");
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "初始化 Foundry Local 失败");
//             // 可根据需要决定是否继续启动
//         }
//
// // 6. 注册管理器单例
//         services.AddSingleton(mgr);
//     }
// }
