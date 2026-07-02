using System.Text.RegularExpressions;
using APromisedLand.Shared.Contracts;
using Elastic.Clients.Elasticsearch;

namespace ElasticsearchService.MessageHandlers;

public class QuestionUpdatedHandler
{
    public async Task HandleAsync(QuestionUpdated message, ElasticsearchClient client)
    {
        var response = await client.UpdateAsync<object, object>("questions", message.QuestionId, u => u
            .Doc(new
            {
                Title = message.Title,
                Content = StripHtml(message.Content),
                Tags = message.Tags
            })
        );

        if (!response.IsValidResponse)
            Console.WriteLine($"更新失败: {response.DebugInformation}");
        else
            Console.WriteLine($"修改 {message.QuestionId} 成功。");
    }

    private static string StripHtml(string content) => Regex.Replace(content, "<.*?>", string.Empty);
}