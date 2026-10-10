namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>
/// 统一 API 响应信封（自 APromisedLand.Shared 迁移）。
/// 所有 NodeEavSky 控制器的成功/失败响应均包装为本结构，
/// 业务载荷放在 <see cref="Data"/>，人类可读信息放在 <see cref="Message"/>。
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, T? data = default) =>
        new() { Success = false, Message = message, Data = data };
}

/// <summary>
/// 统一 API 响应辅助（非泛型，用于无数据返回）。
/// </summary>
public static class ApiResponse
{
    public static ApiResponse<object> Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiResponse<object> Fail(string message) =>
        new() { Success = false, Message = message };
}
