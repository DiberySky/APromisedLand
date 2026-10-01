using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

[ApiController]
[Route("api/units")]
public class UnitsController : ControllerBase
{
    private readonly EavDbContext _db;
    private readonly IUnitCache _unitCache;

    public UnitsController(EavDbContext db, IUnitCache unitCache)
    {
        _db = db;
        _unitCache = unitCache;
    }

    /// <summary>获取全部单位（可按分类过滤）</summary>
    [HttpGet]
    public IActionResult GetAll([FromQuery] string? category)
    {
        var units = category is null
            ? _unitCache.GetAll()
            : _unitCache.GetByCategory(category);

        return Ok(units.Select(u => new
        {
            u.Id,
            u.Category,
            u.Name,
            u.Symbol,
            u.ToBaseFactor,
            u.IsBaseUnit,
            u.DisplayOrder
        }));
    }

    /// <summary>创建单位</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUnitRequest req, CancellationToken ct)
    {
        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            Category = req.Category,
            Name = req.Name,
            Symbol = req.Symbol,
            ToBaseFactor = req.ToBaseFactor,
            IsBaseUnit = req.IsBaseUnit,
            DisplayOrder = req.DisplayOrder
        };
        _db.Units.Add(unit);
        await _db.SaveChangesAsync(ct);
        _unitCache.Invalidate();

        return Ok(new { unit.Id });
    }

    /// <summary>按分类分组，附基准单位信息</summary>
    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        var categories = _unitCache.GetAll()
            .GroupBy(u => u.Category)
            .Select(g => new
            {
                Category = g.Key,
                BaseUnit = g.FirstOrDefault(u => u.IsBaseUnit) is { } b
                    ? new { b.Id, b.Name, b.Symbol }
                    : null,
                Units = g.OrderBy(u => u.DisplayOrder)
                    .Select(u => new { u.Id, u.Name, u.Symbol, u.IsBaseUnit })
            });
        return Ok(categories);
    }
}
