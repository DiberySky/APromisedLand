using APromisedLand.Shared.Contracts;
using Elastic.Clients.Elasticsearch;

namespace ElasticsearchService.MessageHandlers;

public class QuestionDeletedHandler
{
    public async Task HandleAsync(QuestionDeleted message, ElasticsearchClient client)
    {
        var response = await client.DeleteAsync("questions", message.QuestionId);
        if (!response.IsValidResponse)
            Console.WriteLine($"删除失败: {response.DebugInformation}");
        else
            Console.WriteLine($"删除 Id: {message.QuestionId} 成功。");
    }
}