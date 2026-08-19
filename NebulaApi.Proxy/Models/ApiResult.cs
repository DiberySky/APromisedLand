using System.Text.Json;

namespace NebulaApi.Proxy.Models;

/// <summary>
/// Mirror of the FastAPI envelope produced by <c>app.utils.response.ok</c>
/// (<c>{code, message, data, meta?}</c>). The <see cref="Data"/> field is
/// kept as a raw <see cref="JsonElement"/> so the proxy can forward the
/// heterogeneous upstream payloads (spaces, query rows, vertex fetch
/// results, ...) without redefining every response shape.
/// </summary>
public sealed class ApiResult
{
    public int Code { get; set; }
    public string Message { get; set; } = "ok";
    public JsonElement? Data { get; set; }
    public object? Meta { get; set; }
}
