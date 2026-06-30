using APromisedLand.Shared.Contracts;
using SearchService.Models;
using Typesense;

namespace SearchService.MessageHandlers;

public class QuestionDeletedHandler
{
    public async Task HandleAsync(QuestionDeleted message, ITypesenseClient client)
    {
       
        await client.DeleteDocument<SearchQuestion>("questions", message.QuestionId);
        
        Console.WriteLine($"删除Id： {message.QuestionId} 成功。");
    }
}