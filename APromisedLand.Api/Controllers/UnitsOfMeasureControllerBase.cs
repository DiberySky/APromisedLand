using APromisedLand.Api.Interfaces;
using APromisedLand.Shared.DTOs.Shared;
using APromisedLand.Shared.DTOs.Units;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.Controllers;

// [ApiController]
// [Route("[controller]")]
public class UnitsOfMeasureControllerBase(IUnitOfMeasureService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UnitOfMeasureDto>>> GetAll()
    {
        var list = await service.GetAllAsync();
        return Ok(list);
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResponse<UnitOfMeasureDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var request = new PagedRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await service.GetPagedAsync(request);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UnitOfMeasureDto>> GetById(string id)
    {
        var dto = await service.GetByIdAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<UnitOfMeasureDto>> Create(CreateUnitOfMeasureCommand command)
    {
        var dto = await service.CreateAsync(command);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UnitOfMeasureDto>> Update(string id, UpdateUnitOfMeasureCommand command)
    {
        if (id != command.Id)
            return BadRequest("路径 Id 与命令 Id 不匹配");

        var dto = await service.UpdateAsync(command);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}