namespace TreeGraph.Shared.StringTreeSky.Contracts;

public class StringTreeResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static StringTreeResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static StringTreeResponse<T> Fail(string message) => new() { Success = false, Message = message };
}
