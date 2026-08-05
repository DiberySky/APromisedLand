namespace APromisedLand.Shared.DTOs;

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
/// 统一 API 响应辅助（非泛型，用于无数据返回）
/// </summary>
public static class ApiResponse
{
    public static ApiResponse<object> Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiResponse<object> Fail(string message) =>
        new() { Success = false, Message = message };
}