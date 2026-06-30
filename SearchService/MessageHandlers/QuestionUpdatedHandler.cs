using System.Text.RegularExpressions;
using APromisedLand.Shared.Contracts;
using SearchService.Models;
using Typesense;

namespace SearchService.MessageHandlers;

public class QuestionUpdatedHandler
{
    public async Task HandleAsync(QuestionUpdated message, ITypesenseClient client)
    {
        // var doc = new SearchQuestion
        // {
        //     Id = message.QuestionId,
        //     Title = message.Title,
        //     Content = StripHtml(message.Content),
        //     Tags = message.Tags.ToArray()
        // };
        //
        // await client.UpdateDocument("questions", doc.Id, doc);
        // Console.WriteLine($"修改 {doc.Title} Id: {doc.Id} 成功。");
        
        await client.UpdateDocument("questions", message.QuestionId, new
        {
            message.Title,
            Content = StripHtml(message.Content),
            Tags = message.Tags.ToArray()
        });
    }
    
    private static string StripHtml(string content) => Regex.Replace(content, "<.*?>", string.Empty);

}