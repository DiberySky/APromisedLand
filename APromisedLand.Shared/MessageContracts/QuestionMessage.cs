namespace APromisedLand.Shared.MessageContracts;

public class QuestionMessage
{
    public string Operation { get; set; } = "create"; // "create", "update", "delete"
    public required QuestionData Question { get; set; }
}