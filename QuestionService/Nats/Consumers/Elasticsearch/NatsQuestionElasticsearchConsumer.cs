using System.Text.Json;
using System.Text.RegularExpressions;
using APromisedLand.Api.MessageContracts;
using Elastic.Clients.Elasticsearch;
using NATS.Client.Core;
using QuestionService.Services;
using IEmbeddingGenerator = QuestionService.Services.IEmbeddingGenerator;

namespace QuestionService.Nats.Consumers.Elasticsearch;

public class NatsQuestionElasticsearchConsumer(
    NatsConnection nats,
    ElasticsearchClient elastic,
    IEmbeddingGenerator embedder,
    IServiceScopeFactory scopeFactory,          // 改为注入 IServiceScopeFactory
    ILogger<NatsQuestionElasticsearchConsumer> logger) : BackgroundService
{
    private readonly string _subject = "question.events";
    private readonly string _indexName = "questions";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var msg in nats.SubscribeAsync<string>(_subject, cancellationToken: stoppingToken))
        {
            try
            {
                var json = msg.Data;
                if (string.IsNullOrEmpty(json))
                {
                    logger.LogWarning("收到空消息，跳过处理");
                    continue;
                }

                var message = JsonSerializer.Deserialize<QuestionMessage>(json);
                if (message == null) continue;

                // 🔄 为每条消息创建一个独立作用域
                using (var scope = scopeFactory.CreateScope())
                {
                    // 从作用域中获取 IOllamaEmbeddingService
                    // var ollama = scope.ServiceProvider.GetRequiredService<IOllamaEmbeddingService>();

                    switch (message.Operation.ToLower())
                    {
                        case "create":
                            // await IndexOrCreateQuestion(message.Question, ollama, stoppingToken);
                            await IndexOrCreateQuestion(message.Question, stoppingToken);
                            break;
                        case "update":
                            // await IndexOrUpdateQuestion(message.Question, ollama, stoppingToken);
                            await IndexOrUpdateQuestion(message.Question, stoppingToken);
                            break;
                        case "delete":
                            await DeleteQuestion(message.Question.Id, stoppingToken);
                            break;
                        default:
                            logger.LogWarning("未知操作类型: {Operation}", message.Operation);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "处理 NATS 消息失败");
            }
        }
    }

    // 修改方法签名，接收 IOllamaEmbeddingService 参数
    // private async Task IndexOrCreateQuestion(QuestionData question, 
    //     IOllamaEmbeddingService ollama, CancellationToken stoppingToken)
    private async Task IndexOrCreateQuestion(QuestionData question, 
        CancellationToken stoppingToken)
    {
        // var combinedText = $"标题：{question.Title} 内容：{StripHtml(question.Content)}";
        // var embedding = await ollama.GenerateEmbeddingAsync(combinedText);
        
        var doc = new ElasticQuestion
        {
            Id = question.Id,
            Title = question.Title,
            Content = StripHtml(question.Content),
            CreatedAt = question.CreatedAt,
            Tags = question.Tags,
            // 生成向量（若文本过长，可截断至模型最大长度）
            TitleVector = await embedder.GenerateAsync(question.Title),
            ContentVector = await embedder.GenerateAsync(StripHtml(question.Content))
        };

        // var json = System.Text.Json.JsonSerializer.Serialize(doc);
        // logger.LogInformation("待索引文档: {Json}", json);
        
        var response = await elastic.IndexAsync(doc, 
            idx => idx.Index(_indexName).Id(question.Id), 
            stoppingToken);
        if (!response.IsValidResponse)
            logger.LogError("索引失败: {DebugInfo}", response.DebugInformation);
        else
            logger.LogInformation("创建 {Title} 成功，嵌入已存储", doc.Title);
        
        // var mappingResponse = await elastic.Indices.GetMappingAsync<ElasticQuestion>(idx => idx.Index("questions"));
        // var sourceExcludes = mappingResponse.Indices["questions"].Mappings.Source.Excludes;
    }
    
    // private async Task IndexOrUpdateQuestion(QuestionData question, 
    //     IOllamaEmbeddingService ollama, CancellationToken stoppingToken)
    private async Task IndexOrUpdateQuestion(QuestionData question, 
        CancellationToken stoppingToken)
    {
        // var combinedText = $"标题：{question.Title} 内容：{StripHtml(question.Content)}";
        // var embedding = await ollama.GenerateEmbeddingAsync(combinedText);

        var titleVector = await embedder.GenerateAsync(question.Title);
        var contentVector = await embedder.GenerateAsync(question.Content);
        
        var response = await elastic.UpdateAsync<object, object>(_indexName, question.Id, 
            async u => u
            .Doc(new
            {
                question.Title,
                Content = StripHtml(question.Content),
                question.Tags,
                TitleVector = titleVector,
                ContentVector = contentVector
            }), stoppingToken);

        if (!response.IsValidResponse)
            logger.LogError("更新失败: {DebugInfo}", response.DebugInformation);
        else
            logger.LogInformation("修改 {Id} 成功。", question.Id);
    }

    private async Task DeleteQuestion(string questionId, CancellationToken stoppingToken)
    {
        var response = await elastic.DeleteAsync(_indexName, questionId, stoppingToken);
        
        if (!response.IsValidResponse)
            logger.LogError("删除失败: {DebugInfo}", response.DebugInformation);
        else
            logger.LogInformation("删除 Id: {QuestionId} 成功。", questionId);
    }

    private static string StripHtml(string content) => Regex.Replace(content, "<.*?>", string.Empty);
}