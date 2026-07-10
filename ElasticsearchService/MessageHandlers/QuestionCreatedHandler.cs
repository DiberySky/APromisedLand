using System.Text.RegularExpressions;
using APromisedLand.Api.Contracts;
using APromisedLand.Api.Projects.Elasticsearch.Services;
using Elastic.Clients.Elasticsearch;
using ElasticsearchService.Models;

namespace ElasticsearchService.MessageHandlers;

public class QuestionCreatedHandler(IEmbeddingService embeddingService, 
    ILogger<QuestionCreatedHandler> logger)
{
    public async Task HandleAsync(QuestionCreated message, ElasticsearchClient client)
    {
        // 生成嵌入
        var combinedText = $"标题：{message.Title} 内容：{StripHtml(message.Content)}";
        var embedding = await embeddingService.GenerateEmbeddingAsync(combinedText);
        
        var doc = new ElasticQuestion
        {
            Id = message.QuestionId,
            Title = message.Title,
            Content = StripHtml(message.Content),
            CreatedAt = message.Created,
            Tags = message.Tags,
            Embedding = embedding
        };

        var response = await client.IndexAsync(doc, idx => idx.Index("questions").Id(message.QuestionId));
        if (!response.IsValidResponse)
            logger.LogError("索引失败: {DebugInfo}", response.DebugInformation);
        else
            logger.LogInformation("创建 {Title} 成功，嵌入已存储", doc.Title);
    }

    private static string StripHtml(string content) => Regex.Replace(content, "<.*?>", string.Empty);
}

/*
public class QuestionCreatedHandler
{
    public async Task HandleAsync(QuestionCreated message, ElasticsearchClient client)
    {
        var doc = new ElasticQuestion
        {
            Id = message.QuestionId,
            Title = message.Title,
            Content = StripHtml(message.Content),
            CreatedAt = message.Created,
            Tags = message.Tags
        };

        var response = await client.IndexAsync(doc, idx => idx.Index("questions").Id(message.QuestionId));
        if (!response.IsValidResponse)
            Console.WriteLine($"索引失败: {response.DebugInformation}");
        else
            Console.WriteLine($"创建 {doc.Title} Id: {doc.Id} 成功。");
    }

    private static string StripHtml(string content) => Regex.Replace(content, "<.*?>", string.Empty);
}
*/
