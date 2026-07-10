using System.Text.Json;
using APromisedLand.Api.MessageContracts;
using NATS.Client.Core;
using Typesense;

namespace QuestionService.Nats.Consumers.Typesense;

public class NatsQuestionTypesensConsumer(
    NatsConnection nats,
    ITypesenseClient typesense,
    ILogger<NatsQuestionTypesensConsumer> logger)
    : BackgroundService
{
    private readonly string _subject = "question.events";
    private readonly string _collectionName = "questions";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 订阅主题，返回的是 NatsMsg<string>，需使用 .Data 获取消息内容
        await foreach (var msg in nats.SubscribeAsync<string>(_subject, cancellationToken: stoppingToken))
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
                var message = JsonSerializer.Deserialize<QuestionMessage>(json);
                if (message == null) continue;

                switch (message.Operation.ToLower())
                {
                    case "create":
                    case "update":
                        await IndexOrUpdateQuestion(message.Question);
                        break;
                    case "delete":
                        await DeleteQuestion(message.Question.Id);
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

    private async Task DeleteQuestion(string questionId)
    {
        await typesense.DeleteDocument<QuestionData>("questions", questionId);
        logger.LogInformation("已删除问题 {QuestionId}", questionId);
    }

    private async Task IndexOrUpdateQuestion(QuestionData question)
    {
        // 将 QuestionData 转换为 Typesense 文档格式（可直接使用字典或类型）
        var doc = new
        {
            id = question.Id,
            title = question.Title,
            content = question.Content,
            createdAt = question.CreatedAt.ToUnixTimeSeconds(),
            tags = question.Tags,
            hasAcceptedAnswer = question.HasAcceptedAnswer,
            answerCount = question.AnswerCount,
            embedding = question.Embedding,
        };

        // Typesense 的 upsert 操作（文档 id 存在则更新）
        await typesense.CreateDocument(_collectionName, doc);

        logger.LogInformation("已索引问题 {QuestionId}", question.Id);
    }
}