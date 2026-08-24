// using System.Net;
// using System.Net.Http.Json;
// using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
// using APromisedLand.Shared.DTOs;
//
// namespace APromisedLand.MauiBlazor.DiberyTree.Services;
//
// /// <summary>
// /// 定位属性值 API 客户端，调用后端 AttributeLocationValueController（路由 LocationValue）。
// /// 仿 <see cref="TableValueApiClient"/> 分层模式。
// /// </summary>
// public class AttributeLocationValueApiClient(HttpClient httpClient)
// {
//     private const string BasePath = "LocationValue";
//
//     // ---------- 辅助 ----------
//
//     private async Task<TData> SendAndGetDataAsync<TData>(
//         HttpRequestMessage request, CancellationToken cancellationToken = default)
//     {
//         var response = await httpClient.SendAsync(request, cancellationToken);
//         await EnsureSuccessWithApiResponseAsync(response);
//         var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
//         if (apiResponse?.Success != true)
//             throw new Exception(apiResponse?.Message ?? "操作失败，未返回具体错误信息");
//         return apiResponse.Data!;
//     }
//
//     private async Task<TData?> SendAndGetDataOrNullAsync<TData>(
//         HttpRequestMessage request, CancellationToken cancellationToken = default)
//         where TData : class
//     {
//         var response = await httpClient.SendAsync(request, cancellationToken);
//         if (response.StatusCode == HttpStatusCode.NotFound)
//             return null;
//         await EnsureSuccessWithApiResponseAsync(response);
//         var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
//         return apiResponse?.Success == true ? apiResponse.Data : null;
//     }
//
//     private static async Task EnsureSuccessWithApiResponseAsync(HttpResponseMessage response)
//     {
//         if (response.IsSuccessStatusCode)
//             return;
//         try
//         {
//             var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
//             if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
//                 throw new HttpRequestException($"请求失败: {errorResponse.Message}", null, response.StatusCode);
//         }
//         catch
//         {
//             response.EnsureSuccessStatusCode();
//         }
//     }
//
//     // ---------- 定位值 CRUD ----------
//
//     /// <summary>为节点添加定位值，返回完整 DTO。</summary>
//     public async Task<AttributeLocationValueDto> AddAsync(
//         string nodeId, AddAttributeLocationValueDto dto, CancellationToken cancellationToken = default)
//     {
//         var request = new HttpRequestMessage(HttpMethod.Post,
//             $"{BasePath}/node/{Uri.EscapeDataString(nodeId)}")
//         {
//             Content = JsonContent.Create(dto)
//         };
//         return await SendAndGetDataAsync<AttributeLocationValueDto>(request, cancellationToken);
//     }
//
//     /// <summary>获取节点的所有定位值。</summary>
//     public async Task<List<AttributeLocationValueDto>> ListByNodeAsync(
//         string nodeId, CancellationToken cancellationToken = default)
//     {
//         var request = new HttpRequestMessage(HttpMethod.Get,
//             $"{BasePath}/node/{Uri.EscapeDataString(nodeId)}");
//         return await SendAndGetDataAsync<List<AttributeLocationValueDto>>(request, cancellationToken);
//     }
//
//     /// <summary>获取单个定位值（不存在返回 null）。</summary>
//     public async Task<AttributeLocationValueDto?> GetAsync(
//         string valueId, CancellationToken cancellationToken = default)
//     {
//         var request = new HttpRequestMessage(HttpMethod.Get,
//             $"{BasePath}/{Uri.EscapeDataString(valueId)}");
//         return await SendAndGetDataOrNullAsync<AttributeLocationValueDto>(request, cancellationToken);
//     }
//
//     /// <summary>更新定位值，返回更新后的完整 DTO。</summary>
//     public async Task<AttributeLocationValueDto> UpdateAsync(
//         string valueId, UpdateAttributeLocationValueDto dto, CancellationToken cancellationToken = default)
//     {
//         var request = new HttpRequestMessage(HttpMethod.Put,
//             $"{BasePath}/{Uri.EscapeDataString(valueId)}")
//         {
//             Content = JsonContent.Create(dto)
//         };
//         return await SendAndGetDataAsync<AttributeLocationValueDto>(request, cancellationToken);
//     }
//
//     /// <summary>删除定位值（成功返回 true，不存在返回 false）。</summary>
//     public async Task<bool> DeleteAsync(
//         string valueId, CancellationToken cancellationToken = default)
//     {
//         var request = new HttpRequestMessage(HttpMethod.Delete,
//             $"{BasePath}/{Uri.EscapeDataString(valueId)}");
//         var response = await httpClient.SendAsync(request, cancellationToken);
//         if (response.StatusCode == HttpStatusCode.NotFound)
//             return false;
//         await EnsureSuccessWithApiResponseAsync(response);
//         var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
//         return apiResponse?.Success == true && apiResponse.Data;
//     }
// }
