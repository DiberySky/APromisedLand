using System.Text.Json.Serialization;

namespace NebulaApi.Proxy.Dtos;

public sealed class UserCreateIn
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("if_not_exists")]
    public bool IfNotExists { get; set; } = true;
}

public sealed class UserDeleteIn
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("if_exists")]
    public bool IfExists { get; set; } = true;
}

public sealed class PasswordChangeIn
{
    /// <summary>Always set by the controller from the path.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("old_password")]
    public string OldPassword { get; set; } = string.Empty;

    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class PasswordResetIn
{
    /// <summary>Always set by the controller from the path.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Grant a role to a user. Mirrors <c>RoleAssignIn</c>.</summary>
public sealed class RoleAssignIn
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    /// <summary>Space name; omit for GOD role.</summary>
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    /// <summary>GOD | ADMIN | DBA | USER | GUEST</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}

/// <summary>Revoke a role from a user. Mirrors <c>RoleRevokeIn</c>.</summary>
public sealed class RoleRevokeIn
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("space")]
    public string Space { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}
