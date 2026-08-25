using System.Net.Http.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

public partial class AttributeApiClient
{
    /// <summary>根据 ID 获取属性定义（不存在返回 null）</summary>
    public async Task<AttributeLocationDto?> GetLocationAsync(string locationId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/locations/{Uri.EscapeDataString(locationId)}");
        
        return await SendAndGetDataAsync<AttributeLocationDto>(request, cancellationToken);
    }
    
    /// <summary>更新定位值。</summary>
    public async Task<AttributeLocationDto> UpdateLocationAsync(AttributeLocationDto locationDto,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"{BasePath}/locations/{Uri.EscapeDataString(locationDto.LocationId)}")
        {
            Content = JsonContent.Create(locationDto)
        };
        return await SendAndGetDataAsync<AttributeLocationDto>(request, cancellationToken);
    }

}