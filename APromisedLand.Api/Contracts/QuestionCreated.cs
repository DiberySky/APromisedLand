namespace APromisedLand.Api.Contracts;

public record QuestionCreated(string QuestionId, string Title, string Content,
    DateTimeOffset Created, List<string> Tags);