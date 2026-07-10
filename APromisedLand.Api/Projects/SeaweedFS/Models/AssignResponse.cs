using System.Text.Json.Serialization;

namespace APromisedLand.Api.Projects.SeaweedFS.Models;

public class AssignResponse
{
    [JsonPropertyName("fileId")]
    public string FileId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string PublicUrl { get; set; } = string.Empty;
}