namespace APromisedLand.Api.NebulaGraph.Models;

/// <summary>
/// Outcome of a single proxied call to the FastAPI + nebula-python
/// service: carries the upstream HTTP status code together with the
/// parsed <see cref="ApiResult"/> envelope (or a fallback envelope on
/// transport-level failures). Controllers surface this directly to the
/// client via <c>StatusCode(StatusCode, Body)</c>.
/// </summary>
public sealed class ProxyResult
{
    public required int StatusCode { get; init; }
    public required ApiResult Body { get; init; }

    public bool IsSuccessStatusCode => StatusCode is >= 200 and < 300;

    public static ProxyResult Ok(ApiResult body) => new()
    {
        StatusCode = 200,
        Body = body,
    };

    /// <summary>Used when the upstream service returns a non-2xx status;
    /// the FastAPI envelope is forwarded to the caller as-is.</summary>
    public static ProxyResult Upstream(int statusCode, ApiResult body) => new()
    {
        StatusCode = statusCode,
        Body = body,
    };

    /// <summary>Used when the proxy itself fails to reach the upstream
    /// service (DNS, connection refused, timeout, JSON parse error,
    /// ...). Produces a 502 Bad Gateway with a synthetic envelope.</summary>
    public static ProxyResult BadGateway(string message, object? data = null) => new()
    {
        StatusCode = 502,
        Body = new ApiResult { Code = 502, Message = message, Data = data is null ? null : System.Text.Json.JsonSerializer.SerializeToElement(data) },
    };
}
