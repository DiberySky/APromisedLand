using APromisedLand.Api.MessageContracts;

namespace QuestionService.Nats.Publishers;

public interface IQuestionPublisher
{
    Task PublishQuestionAsync(QuestionData question, string operation);
}