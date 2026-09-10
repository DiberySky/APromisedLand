using MAFRagService.Agents;
using MAFRagService.Connectors;
using MAFRagService.Memory;
using MAFRagService.Startup.Configuration;
using MAFRagService.Stubs.MAF;
using MAFRagService.Tools;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace MAFRagService.Startup.Extensions;

public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddRagAiServices(
        this IServiceCollection services,
        IConfiguration config,
        ConnectionStrings conns)
    {
        // ---------- 解析 Ollama 端点（优先级：chat → embedding → 根 → 配置 → 默认）----------
        var chatConn  = OllamaEndpoint.Parse(config.GetConnectionString("chat-model"));
        var embedConn = OllamaEndpoint.Parse(config.GetConnectionString("embedding"));
        var rootConn  = OllamaEndpoint.Parse(config.GetConnectionString("Ollama"));

        var url = OllamaEndpoint.Coalesce(
            "http://localhost:11434",
            chatConn.Url, embedConn.Url, rootConn.Url,
            config["Ollama:Url"]);

        var chatModel = OllamaEndpoint.Coalesce(
            "qwen2.5:7b",
            chatConn.Model, config["Ollama:Model"]);

        var embedModel = OllamaEndpoint.Coalesce(
            "bge-large",
            embedConn.Model, config["Ollama:EmbeddingModel"]);

        // 用已解析的值覆盖 IOptions，让下游服务直接注入 OllamaOptions 即可
        services.PostConfigure<OllamaOptions>(opt =>
        {
            opt.Url            = url;
            opt.ChatModel      = chatModel;
            opt.EmbeddingModel = embedModel;
        });

        // ---------- MAF 核心 ----------
        services.AddAgentFramework();

        // ---------- Ollama HttpClient ----------
        services.AddHttpClient(HttpClientNames.Ollama, (_, http) =>
        {
            http.BaseAddress = new Uri(url);
            // 首次加载大模型可能远超 60s，给足时间
            http.Timeout = TimeSpan.FromMinutes(5);
        })
        .AddPolicyHandler((sp, _) =>
        {
            var opt = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;

            var retry = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount: opt.RetryCount,
                    sleepDurationProvider: attempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (_, delay, attempt, _) =>
                        sp.GetRequiredService<ILogger<OllamaModelConnector>>()
                          .LogWarning("Ollama retry {Attempt} after {Delay}s",
                                      attempt, delay.TotalSeconds));

            var timeout = Policy.TimeoutAsync<HttpResponseMessage>(opt.PolicyTimeout);
            return Policy.WrapAsync(retry, timeout);
        });

        services.AddSingleton<IAgentModel>(sp =>
        {
            var opt    = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            var http   = sp.GetRequiredService<IHttpClientFactory>()
                           .CreateClient(HttpClientNames.Ollama);
            var logger = sp.GetRequiredService<ILogger<Program>>();

            logger.LogInformation(
                "Ollama 已配置：Url={Url}, ChatModel={Chat}, EmbeddingModel={Embed}",
                opt.Url, opt.ChatModel, opt.EmbeddingModel);

            return new OllamaModelConnector(http, opt.Url, opt.ChatModel);
        });

        // ---------- Weaviate HttpClient ----------
        services.AddHttpClient(HttpClientNames.Weaviate, (sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<WeaviateOptions>>().Value;
            client.BaseAddress = new Uri(conns.Weaviate);
            client.Timeout     = opt.RequestTimeout;
        })
        .AddPolicyHandler((sp, _) =>
        {
            var opt = sp.GetRequiredService<IOptions<WeaviateOptions>>().Value;

            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(opt.RetryCount,
                    retry => TimeSpan.FromSeconds(retry * 2));
        });

        // ---------- 工具与智能体 ----------
        services.AddScoped<DocumentSearchTool>();
        services.AddScoped<GraphQueryTool>();
        services.AddScoped<EntityExtractionTool>();
        services.AddScoped<StorageTool>();

        services.AddTransient<OrchestratorAgent>();
        services.AddTransient<QueryAnalyzerAgent>();
        services.AddTransient<RetrieverAgent>();
        services.AddTransient<GraphReasonerAgent>();
        services.AddTransient<AnswerGeneratorAgent>();

        services.AddSingleton<INebulaGraphMemoryStore, NebulaGraphMemoryStore>();
        services.AddSingleton<IMemoryStore>(sp =>
            sp.GetRequiredService<INebulaGraphMemoryStore>());

        return services;
    }
}
