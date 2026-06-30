using System.Text.RegularExpressions;
using APromisedLand.Shared.Contracts;
using SearchService.Models;
using Typesense;

namespace SearchService.MessageHandlers;

public class QuestionCreatedHandler
{
    // 移除构造函数，或保留无参构造函数
    public async Task HandleAsync(QuestionCreated message, ITypesenseClient client)
    {
        var doc = new SearchQuestion
        {
            Id = message.QuestionId,
            Title = message.Title,
            Content = StripHtml(message.Content),
            CreatedAt = message.Created.ToUnixTimeMilliseconds(),
            Tags = message.Tags.ToArray()
        };
        
        await client.CreateDocument("questions", doc);
        
        Console.WriteLine($"创建 {doc.Title} Id: {doc.Id} 成功。");
    }

    private static string StripHtml(string content) => Regex.Replace(content, "<.*?>", string.Empty);
}

// public class QuestionCreatedHandler(ITypesenseClient client)
// {
//     public async Task Handle(QuestionCreated message)
//     {
//         // var created = message.Created.ToUnixTimeMilliseconds();
//
//         var doc = new SearchQuestion
//         {
//             Id = message.QuestionId,
//             Title = message.Title,
//             Content = StripHtml(message.Content),
//             CreatedAt = message.Created.ToUnixTimeMilliseconds(),
//             Tags = message.Tags.ToArray()
//         };
//         
//         await client.CreateDocument("questions", doc);
//         
//         Console.WriteLine($"创建 {doc.Title} 用 id {doc.Id}");
//     }
//
//     private static string StripHtml(string content)
//     {
//         return Regex.Replace(content, "<.*?>", string.Empty);
//     }
// }