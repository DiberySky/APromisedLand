using APromisedLand.Shared.MessageContracts;

namespace QuestionService.Services.Nats;

public interface IQuestionPublisher
{
    Task PublishQuestionAsync(QuestionData question, string operation);
}