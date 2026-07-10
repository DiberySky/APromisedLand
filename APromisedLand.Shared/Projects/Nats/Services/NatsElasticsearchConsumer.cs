using System.Text.Json;
using System.Text.RegularExpressions;
using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Projects.Elasticsearch.Embedding;
using APromisedLand.Shared.Projects.Nats.Models;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace APromisedLand.Shared.Projects.Nats.Services;

public class NatsElasticsearchConsumer(
    NatsConnection nats,
    ElasticsearchClient elastic,
    IEmbeddingGenerator embedder,
    IServiceScopeFactory scopeFactory, // 改为注入 IServiceScopeFactory
    ILogger<NatsElasticsearchConsumer> logger) : BackgroundService
{
    private const string Subject = SolutionHelper.NatsDocumentSubject;
    private const string IndexName = SolutionHelper.ElasticDataIndexName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var msg in nats.SubscribeAsync<string>(Subject, cancellationToken: stoppingToken))
        {
            try
            {
                var json = msg.Data;
                if (string.IsNullOrEmpty(json))
                {
                    logger.LogWarning("收到空消息，跳过处理");
                    continue;
                }

                var message = JsonSerializer.Deserialize<DocumentMessage>(json);
                if (message == null) continue;

                // 🔄 为每条消息创建一个独立作用域
                using var scope = scopeFactory.CreateScope();
                switch (message.Operation)
                {
                    case NatsOperation.Create:
                        await IndexOrCreateQuestion(message.Document, stoppingToken);
                        break;
                    case NatsOperation.Update:
                        // await IndexOrUpdateQuestion(message.Question, ollama, stoppingToken);
                        await IndexOrUpdateQuestion(message.Document, stoppingToken);
                        break;
                    case NatsOperation.Delete:
                        await DeleteQuestion(message.Document.Id, stoppingToken);
                        break;
                    default:
                        logger.LogWarning("未知操作类型: {Operation}", message.Operation);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "处理 NATS 消息失败");
            }
        }
    }

    // 修改方法签名，接收 IOllamaEmbeddingService 参数
    private async Task IndexOrCreateQuestion(DocumentData documentData,
        CancellationToken stoppingToken)
    {
        var doc = new DocumentData()
        {
            Id = documentData.Id,
            Title = documentData.Title,
            Content = StripHtml(documentData.Content),
            // 生成向量（若文本过长，可截断至模型最大长度）
            TitleVector = await embedder.GenerateAsync(documentData.Title),
            ContentVector = await embedder.GenerateAsync(StripHtml(documentData.Content))
        };
        
        var response = await elastic.IndexAsync(doc,
            idx => idx.Index(IndexName).Id(documentData.Id),
            stoppingToken);
        if (!response.IsValidResponse)
            logger.LogError("索引失败: {DebugInfo}", response.DebugInformation);
        else
            logger.LogInformation("创建 {Title} 成功，嵌入已存储", doc.Title);
    }

    private async Task IndexOrUpdateQuestion(DocumentData documentData,
        CancellationToken stoppingToken)
    {
        var titleVector = await embedder.GenerateAsync(documentData.Title);
        var contentVector = await embedder.GenerateAsync(documentData.Content);

        var response = await elastic.UpdateAsync<object, object>(IndexName, documentData.Id,
            async u => u
                .Doc(new
                {
                    documentData.Title,
                    Content = StripHtml(documentData.Content),
                    TitleVector = titleVector,
                    ContentVector = contentVector
                }), stoppingToken);

        if (!response.IsValidResponse)
            logger.LogError("更新失败: {DebugInfo}", response.DebugInformation);
        else
            logger.LogInformation("修改 {Id} 成功。", documentData.Id);
    }

    private async Task DeleteQuestion(string documentId, CancellationToken stoppingToken)
    {
        var response = await elastic.DeleteAsync(IndexName, documentId, stoppingToken);

        if (!response.IsValidResponse)
            logger.LogError("删除失败: {DebugInfo}", response.DebugInformation);
        else
            logger.LogInformation("删除 Id: {QuestionId} 成功。", documentId);
    }

    private static string StripHtml(string content) => Regex.Replace(content, "<.*?>", string.Empty);
}