namespace MAFRagService.Models;

public class Source
{
    public string DocId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float Score { get; set; }
}
