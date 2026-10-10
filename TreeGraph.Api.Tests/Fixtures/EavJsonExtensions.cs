using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// NodeEavSky 统一 ApiResponse 信封的测试解包扩展。
/// 仅用于 EAV / inode / units 端点；StringTree 等非信封端点仍用原生 ReadFromJsonAsync。
/// </summary>
public static class EavJsonExtensions
{
    public static async Task<T?> ReadEavAsync<T>(
        this HttpContent content,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default) where T : class
        => (await content.ReadFromJsonAsync<ApiResponse<T>>(options, cancellationToken))?.Data;

    public static async Task<T?> GetEavAsync<T>(
        this HttpClient client,
        string? requestUri,
        CancellationToken cancellationToken = default) where T : class
        => (await client.GetFromJsonAsync<ApiResponse<T>>(requestUri, cancellationToken))?.Data;
}
