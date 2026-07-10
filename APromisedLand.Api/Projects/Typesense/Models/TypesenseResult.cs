namespace APromisedLand.Api.Projects.Typesense.Models;

public class TypesenseResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}