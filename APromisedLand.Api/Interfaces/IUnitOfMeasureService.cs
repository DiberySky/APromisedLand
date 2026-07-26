using APromisedLand.Shared.DTOs.Shared;
using APromisedLand.Shared.DTOs.Units;

namespace APromisedLand.Api.Interfaces;

public interface IUnitOfMeasureService
{
    Task<UnitOfMeasureDto> CreateAsync(CreateUnitOfMeasureCommand command);
    Task<UnitOfMeasureDto?> GetByIdAsync(string id);
    Task<List<UnitOfMeasureDto>> GetAllAsync();
    Task<PagedResponse<UnitOfMeasureDto>> GetPagedAsync(PagedRequest request);
    Task<UnitOfMeasureDto?> UpdateAsync(UpdateUnitOfMeasureCommand command);
    Task<bool> DeleteAsync(string id);
}