using System.ComponentModel.DataAnnotations;

namespace NebulaApi.Proxy.Models;

/// <summary>
/// Strongly-typed options bound to the <c>NebulaApi</c> configuration
/// section. These describe how the .NET proxy reaches the upstream
/// FastAPI + nebula-python service.
/// </summary>
public sealed class NebulaFastApiOptions
{
    /// <summary>Base URL of the deployed FastAPI service, e.g.
    /// <c>http://127.0.0.1:8000</c>. Must not include a trailing
    /// slash.</summary>
    [Required]
    public string BaseUrl { get; set; } = "https://localhost:9339";

    /// <summary>Optional bearer token. When non-empty, the proxy
    /// forwards it as <c>Authorization: Bearer &lt;token&gt;</c> so the
    /// upstream FastAPI <c>verify_token</c> guard accepts the request.
    /// Leave empty when the upstream guard is disabled.</summary>
    public string? ApiToken { get; set; }

    /// <summary>Per-request timeout in seconds applied to the
    /// <c>HttpClient</c> used for upstream calls.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>When <c>true</c> (default), upstream 4xx/5xx envelopes
    /// are forwarded to the client verbatim. When <c>false</c>, the
    /// proxy normalises any non-2xx upstream response into a 502 Bad
    /// Gateway envelope.</summary>
    public bool ForwardUnauthorized { get; set; } = true;
}
