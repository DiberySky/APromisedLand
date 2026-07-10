using System.Text.Json;
using APromisedLand.Shared.Helper;
using APromisedLand.Shared.MessageContracts;
using APromisedLand.Shared.Projects.Nats.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Typesense;

namespace APromisedLand.Shared.Projects.Typesense.Nats;

public class NatsTypesenseConsumer(
    NatsConnection nats,
    ITypesenseClient typesense,
    ILogger<NatsTypesenseConsumer> logger)
    : BackgroundService
{
    private const string Subject = SolutionHelper.NatsDocumentSubject;
    private const string CollectionName = SolutionHelper.TypesenseCollectionName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 订阅主题，返回的是 NatsMsg<string>，需使用 .Data 获取消息内容
        await foreach (var msg in nats.SubscribeAsync<string>(Subject, cancellationToken: stoppingToken))
        {
            try
            {
                // 从 NatsMsg 中提取 JSON 字符串
                // 1. 提取 JSON 字符串并检查空值
                var json = msg.Data;
                if (string.IsNullOrEmpty(json))
                {
                    logger.LogWarning("收到空消息，跳过处理");
                    continue;
                }

                // 2. 反序列化（此时 json 非 null）
                var message = JsonSerializer.Deserialize<DocumentMessage>(json);
                if (message == null) continue;

                switch (message.Operation)
                {
                    case NatsOperation.Create:
                    case NatsOperation.Update:
                        await IndexOrUpdateDocument(message.Document);
                        break;
                    case NatsOperation.Delete:
                        await DeleteDocument(message.Document.Id);
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

    private async Task DeleteDocument(string documentId)
    {
        await typesense.DeleteDocument<DocumentData>(CollectionName, documentId);
        logger.LogInformation("已删除文档 {DocumentId}", documentId);
    }

    private async Task IndexOrUpdateDocument(DocumentData document)
    {
        // 将 DocumentData 转换为 Typesense 文档格式（可直接使用字典或类型）
        var doc = new
        {
            id = document.Id,
            title = document.Title,
            content = document.Content,
        };

        // Typesense 的 upsert 操作（文档 id 存在则更新）
        await typesense.CreateDocument(CollectionName, doc);

        logger.LogInformation("已索引文档 {QuestionId}", document.Id);
    }
}