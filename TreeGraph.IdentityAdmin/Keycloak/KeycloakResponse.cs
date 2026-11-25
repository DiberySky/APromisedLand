using System.Text.Json;

namespace TreeGraph.IdentityAdmin.Keycloak;

/// <summary>Keycloak Admin API 原始响应。</summary>
public sealed class KeycloakResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KeycloakResponse(int statusCode, string body, string? location)
    {
        StatusCode = statusCode;
        Body = body;
        Location = location;
    }

    public int StatusCode { get; }

    public string Body { get; }

    public string? Location { get; }

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public T? Deserialize<T>()
    {
        if (string.IsNullOrWhiteSpace(Body))
            return default;

        return JsonSerializer.Deserialize<T>(Body, JsonOptions);
    }

    /// <summary>从创建响应的 Location 头提取新资源 ID。</summary>
    public string? ExtractCreatedId()
    {
        if (string.IsNullOrWhiteSpace(Location))
            return null;

        var trimmed = Location.TrimEnd('/');
        var index = trimmed.LastIndexOf('/');
        return index >= 0 && index < trimmed.Length - 1
            ? trimmed[(index + 1)..]
            : null;
    }
}
