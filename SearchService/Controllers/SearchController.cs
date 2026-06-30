using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SearchService.Models;
using Typesense;

namespace SearchService.Controllers;
//
// [ApiController]
// [Route("[controller]")]
// public class SearchController(ITypesenseClient client): ControllerBase
// {
//     [HttpGet("{query}")]
//     public async Task<ActionResult<IEnumerable<SearchQuestion>>> Get(string query)
//     {
//         // [aspire]something
//         string? tag = null;
//         var tagMatch = Regex.Match(query, @"\[(.*?)\]");
//         if (tagMatch.Success)
//         {
//             tag = tagMatch.Groups[1].Value;
//             query = query.Replace(tagMatch.Value, "").Trim();
//         }
//     
//         var searchParams = new SearchParameters(query, "title,content");
//     
//         if (!string.IsNullOrWhiteSpace(tag))
//         {
//             searchParams.FilterBy = $"tags:=[{tag}]]";
//         }
//     
//         try
//         {
//             var result = await client.Search<SearchQuestion>("questions", searchParams);
//             return Ok(result.Hits.Select(hit => hit.Document));
//         }
//         catch (Exception e)
//         {
//             return Problem("Typesense 搜索失败", e.Message);
//         }
//     }
//
//     // [HttpGet]
//     // public async Task<ActionResult<IResult>> Get(string query)
//     // {
//     //     //[aspire]something
//     //     string? tag = null;
//     //     var tagMatch = Regex.Match(query, @"\[(.*?)\]");
//     //     if (tagMatch.Success)
//     //     {
//     //         tag = tagMatch.Groups[1].Value;
//     //         query = query.Replace(tagMatch.Value, "").Trim();
//     //     }
//     //
//     //     var searchParams = new SearchParameters(query, "title,content");
//     //
//     //     if (!string.IsNullOrWhiteSpace(tag))
//     //     {
//     //         searchParams.FilterBy = $"tags:=[{tag}]]";
//     //     }
//     //
//     //     try
//     //     {
//     //         var result = await client.Search<SearchQuestion>("questions", searchParams);
//     //         var results = Results.Ok(result.Hits.Select(hit => hit.Document));
//     //         return Results.Ok(result.Hits.Select(hit => hit.Document));
//     //     }
//     //     catch (Exception e)
//     //     {
//     //         return Results.Problem("Typesense 搜索失败", e.Message);
//     //     }
//     // }
// }