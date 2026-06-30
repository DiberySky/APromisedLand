using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SearchService.Models;
using Typesense;

namespace SearchService.Controllers;

[ApiController]
[Route("[controller]")]
public class TypesenseController(ITypesenseClient typesenseClient) : ControllerBase
{
    // GET /api/typesense?query=xxx
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        string? tag = null;
        var tagMatch = Regex.Match(query, @"\[(.*?)\]");
        if (tagMatch.Success)
        {
            tag = tagMatch.Groups[1].Value;
            query = query.Replace(tagMatch.Value, "").Trim();
        }

        var searchParams = new SearchParameters(query, "title,content");

        if (!string.IsNullOrWhiteSpace(tag))
        {
            // 原代码里 FilterBy 有多余的 ]，此处保持与最小 API 一致
            searchParams.FilterBy = $"tags:=[{tag}]]";
        }

        try
        {
            var result = await typesenseClient.Search<SearchQuestion>("questions", searchParams);
            return Ok(result.Hits.Select(hit => hit.Document));
        }
        catch (Exception e)
        {
            return Problem("Typesense 搜索失败", e.Message);
        }
    }

    // GET /api/typesense/similar-titles?query=xxx
    [HttpGet("similar-titles")]
    public async Task<IActionResult> SimilarTitles([FromQuery] string query)
    {
        var searchParams = new SearchParameters(query, "title");

        try
        {
            var result = await typesenseClient.Search<SearchQuestion>("questions", searchParams);
            return Ok(result.Hits.Select(hit => hit.Document));
        }
        catch (Exception e)
        {
            return Problem("Typesense 搜索失败", e.Message);
        }
    }
}