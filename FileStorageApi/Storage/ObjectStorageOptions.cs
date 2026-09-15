using System.ComponentModel.DataAnnotations;

namespace FileStorageApi.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "SeaweedFS";

    [Required, Url]
    public string Endpoint   { get; set; } = "http://localhost:8333";

    [Required]
    public string AccessKey  { get; set; } = "admin";

    [Required]
    public string SecretKey  { get; set; } = "admin";

    [Required, MinLength(1), MaxLength(63)]
    public string Bucket     { get; set; } = "documents";

    public bool   ForcePathStyle { get; set; } = true;
    public bool   UseHttp        { get; set; } = true;

    [Required]
    public string Region         { get; set; } = "us-east-1";
}