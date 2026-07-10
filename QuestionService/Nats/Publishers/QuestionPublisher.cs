using System.Text.Json;
using APromisedLand.Api.MessageContracts;
using NATS.Client.Core;

namespace QuestionService.Nats.Publishers;

public class QuestionPublisher(NatsConnection nats) : IQuestionPublisher
{
    private readonly string _subject = "question.events";

    public async Task PublishQuestionAsync(QuestionData question, string operation)
    {
        var message = new QuestionMessage
        {
            Operation = operation,
            Question = question
        };
        
        var json = JsonSerializer.Serialize(message);
        
        await nats.PublishAsync(_subject, json);
    }
}