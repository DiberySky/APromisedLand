using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiberyBlazorWebSky.Models.Mcp;

public class McpRequest
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public int Id { get; set; } = 1;
    [JsonPropertyName("method")] public string Method { get; set; } = "";
    [JsonPropertyName("params")] public object? Params { get; set; }
}

public class McpResponse<T>
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "";
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("result")] public T? Result { get; set; }
    [JsonPropertyName("error")] public McpError? Error { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("data")] public JsonElement? Data { get; set; }
}

public class McpToolListResult
{
    [JsonPropertyName("tools")] public List<McpTool> Tools { get; set; } = new();
}

public class McpTool
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("inputSchema")] public JsonElement InputSchema { get; set; }
}

public class McpCallToolResult
{
    [JsonPropertyName("content")] public List<McpContent> Content { get; set; } = new();
    [JsonPropertyName("isError")] public bool IsError { get; set; }
}

public class McpContent
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("text")] public string? Text { get; set; }
}