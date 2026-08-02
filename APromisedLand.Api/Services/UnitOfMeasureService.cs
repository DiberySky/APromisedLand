using APromisedLand.Api.Data;
using APromisedLand.Api.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DTOs.Shared;
using APromisedLand.Shared.DTOs.Units;
using APromisedLand.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace APromisedLand.Api.Services;

public class UnitOfMeasureService(DiberyDbContext context) : IUnitOfMeasureService
{
    public async Task<UnitOfMeasureDto> CreateAsync(CreateUnitOfMeasureCommand command)
    {
        var existing = await context.UnitsOfMeasure
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Name == command.Name);

        if (existing is not null)
            return MapToDto(existing);

        var entity = new UnitOfMeasure
        {
            Id = Guid.NewGuid().ToString(),
            Name = command.Name,
            Symbol = command.Symbol,
            Description = command.Description,
            IsActive = true
        };

        context.UnitsOfMeasure.Add(entity);
        await context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<UnitOfMeasureDto?> GetByIdAsync(string id)
    {
        var entity = await context.UnitsOfMeasure.FindAsync(id);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<List<UnitOfMeasureDto>> GetAllAsync()
    {
        var entities = await context.UnitsOfMeasure
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync();
        return entities.Select(MapToDto).ToList();
    }

    public async Task<PagedResponse<UnitOfMeasureDto>> GetPagedAsync(PagedRequest request)
    {
        var totalCount = await context.UnitsOfMeasure.CountAsync();

        var items = await context.UnitsOfMeasure
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UnitOfMeasureDto
            {
                Id = u.Id,
                Name = u.Name,
                Symbol = u.Symbol,
                Description = u.Description,
                IsActive = u.IsActive
            })
            .ToListAsync();

        return new PagedResponse<UnitOfMeasureDto>
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<UnitOfMeasureDto?> UpdateAsync(UpdateUnitOfMeasureCommand command)
    {
        var entity = await context.UnitsOfMeasure.FindAsync(command.Id);
        if (entity is null) return null;

        entity.Name = command.Name;
        entity.Symbol = command.Symbol;
        entity.Description = command.Description;
        entity.IsActive = command.IsActive;

        await context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await context.UnitsOfMeasure.FindAsync(id);
        if (entity is null) return false;

        context.UnitsOfMeasure.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    private static UnitOfMeasureDto MapToDto(UnitOfMeasure entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Symbol = entity.Symbol,
        Description = entity.Description,
        IsActive = entity.IsActive
    };
}