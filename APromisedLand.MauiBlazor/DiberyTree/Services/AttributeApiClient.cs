// AttributeApiClient.cs
using System.Net;
using System.Net.Http.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DTOs;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

/// <summary>
/// 属性定义 API 客户端，调用后端的 <see cref="APromisedLand.Api.Projects.DiberyTree.AttributeControllerBase"/>
/// （派生 AttributesController → 路由 attributes/definitions）。
/// <para>属性定义为全局资源，不耦合具体树，故无 _basePath 前缀。</para>
/// </summary>
public partial class AttributeApiClient(HttpClient httpClient)
{
    private const string BasePath = "attributes";

    // ---------- 辅助 ----------

    private async Task<TData> SendAndGetDataAsync<TData>(
        HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        
        await EnsureSuccessWithApiResponseAsync(response);
        
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
        if (apiResponse?.Success != true)
            throw new Exception(apiResponse?.Message ?? "操作失败，未返回具体错误信息");

        return apiResponse.Data!;
    }

    private async Task<TData?> SendAndGetDataOrNullAsync<TData>(
        HttpRequestMessage request, CancellationToken cancellationToken = default)
        where TData : class
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        
        await EnsureSuccessWithApiResponseAsync(response);
        
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
        
        return apiResponse?.Success == true ? apiResponse.Data : null;
    }

    private static async Task EnsureSuccessWithApiResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        // 尝试提取业务错误信息
        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            // 只要有业务错误信息就用它，没有就用 HTTP 默认原因
            string message = errorResponse?.Message;
            if (string.IsNullOrEmpty(message))
            {
                message = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
            }
            throw new HttpRequestException($"请求失败: {message}", null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            // 如果是上面手动抛出的，直接重新抛出
            throw;
        }
        catch
        {
            // 反序列化或其他异常发生，回退到默认的 EnsureSuccessStatusCode 兜底
            response.EnsureSuccessStatusCode();
        }
    }
}
