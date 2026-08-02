namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

public class FileAttributeValue : AttributeValueBase
{
    public string Value { get; set; } = null!;        // 文件路径/URL
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? Size { get; set; }
}