// /Configuration/SeaweedFsOptions.cs
using System.ComponentModel.DataAnnotations;

namespace MAFRagService.Startup.Configuration;

public sealed class SeaweedFsOptions
{
    public const string SectionName = "SeaweedFS";

    [Required] public string AccessKey { get; set; } = "dummy";
    [Required] public string SecretKey { get; set; } = "dummy";
    [Required, MinLength(3)] public string Region { get; set; } = "us-east-1";
}