namespace MAFRagService.Startup.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string? DevFallback { get; set; } = "DevSecretKey123!@#";
}
