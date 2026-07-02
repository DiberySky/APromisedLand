using System.Text.RegularExpressions;
using APromisedLand.Shared.Contracts;
using Elastic.Clients.Elasticsearch;
using ElasticsearchService.Models;

namespace ElasticsearchService.MessageHandlers;

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