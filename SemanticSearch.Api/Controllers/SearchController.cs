using Microsoft.AspNetCore.Mvc;
using SemanticSearch.Api.Services;

namespace SemanticSearch.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class SearchController(ElasticsearchService esService) : ControllerBase
{
    [HttpGet("semantic")]
    public async Task<IActionResult> SemanticSearch([FromQuery] string q, [FromQuery] int size = 5)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("查询词不能为空");

        var results = await esService.SemanticSearchAsync(q, size);
        return Ok(results);
    }
    
    [HttpGet("text")]
    public async Task<IActionResult> TextSearch([FromQuery] string q, [FromQuery] int size = 5)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("查询词不能为空");

        var results = await esService.TextSearchAsync(q, size);
        return Ok(results);
    }
    
    [HttpGet("hybrid")]
    public async Task<IActionResult> HybridSearch(
        [FromQuery] string q,
        [FromQuery] int size = 5,
        [FromQuery] float knnBoost = 1.0f,
        [FromQuery] float bm25Boost = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("查询词不能为空");

        var results = await esService.HybridSearchAsync(q, size, knnBoost, bm25Boost);
        return Ok(results);
    }
}