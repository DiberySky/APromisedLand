using System.Text.Json;
using System.Text.Json.Nodes;

namespace TreeGraph.IdentityAdmin.Auditing;

/// <summary>
/// 审计入参脱敏：对 secret / password / credential / token 类字段统一替换为占位符。
/// </summary>
public static class AdminRequestSanitizer
{
    private const string Masked = "***";

    private static readonly string[] SensitiveFragments =
    [
        "secret",
        "password",
        "credential",
        "token"
    ];

    public static string? Serialize(object? request)
    {
        if (request is null)
            return null;

        try
        {
            var node = JsonSerializer.SerializeToNode(request);
            Mask(node);
            return node?.ToJsonString();
        }
        catch (Exception)
        {
            return "{\"note\":\"request serialization failed\"}";
        }
    }

    private static void Mask(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var keys = obj.Select(p => p.Key).ToArray();
                foreach (var key in keys)
                {
                    if (IsSensitive(key))
                    {
                        obj[key] = Masked;
                    }
                    else
                    {
                        Mask(obj[key]);
                    }
                }

                break;
            }

            case JsonArray array:
            {
                foreach (var item in array)
                    Mask(item);

                break;
            }
        }
    }

    private static bool IsSensitive(string key)
        => SensitiveFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
