using System.Net.Http.Json;
using APromisedLand.Shared.DTOs.Shared;
using APromisedLand.Shared.DTOs.Units;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

public class UnitsOfMeasureApiClient(HttpClient httpClient)
{
    // 获取全部（不分页）
    public async Task<List<UnitOfMeasureDto>> GetAllAsync()
    {
        var result = await httpClient.GetFromJsonAsync<List<UnitOfMeasureDto>>("/UnitsOfMeasure");
        return result ?? new List<UnitOfMeasureDto>();
    }

    // 分页获取
    public async Task<PagedResponse<UnitOfMeasureDto>> GetPagedAsync(int pageNumber = 1, int pageSize = 20)
    {
        var url = $"/UnitsOfMeasure/paged?pageNumber={pageNumber}&pageSize={pageSize}";
        var result = await httpClient.GetFromJsonAsync<PagedResponse<UnitOfMeasureDto>>(url);
        return result ?? new PagedResponse<UnitOfMeasureDto>();
    }

    // 按 ID 获取单个
    public async Task<UnitOfMeasureDto?> GetByIdAsync(string id)
    {
        return await httpClient.GetFromJsonAsync<UnitOfMeasureDto>($"/UnitsOfMeasure/{id}");
    }

    // 新增（存在则返回已有记录）
    public async Task<UnitOfMeasureDto?> CreateAsync(CreateUnitOfMeasureCommand command)
    {
        var response = await httpClient.PostAsJsonAsync("/UnitsOfMeasure", command);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UnitOfMeasureDto>();
    }

    // 更新
    public async Task<UnitOfMeasureDto?> UpdateAsync(string id, UpdateUnitOfMeasureCommand command)
    {
        var response = await httpClient.PutAsJsonAsync($"/UnitsOfMeasure/{id}", command);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UnitOfMeasureDto>();
    }

    // 删除
    public async Task<bool> DeleteAsync(string id)
    {
        var response = await httpClient.DeleteAsync($"/UnitsOfMeasure/{id}");
        return response.IsSuccessStatusCode;
    }
}