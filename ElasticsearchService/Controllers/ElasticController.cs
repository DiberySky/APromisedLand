using System.Text.RegularExpressions;
using APromisedLand.Shared.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ElasticsearchService.Models;
using Microsoft.AspNetCore.Mvc;

namespace ElasticsearchService.Controllers;

[ApiController]
[Route("[controller]")]
public class ElasticController(ElasticsearchClient client) : ControllerBase
{
    // GET /elastic?query=xxx  （支持 [tag] 过滤）
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        // 解析标签（同 Typesense 逻辑）
        string? tag = null;
        var tagMatch = Regex.Match(query, @"\[(.*?)\]");
        if (tagMatch.Success)
        {
            tag = tagMatch.Groups[1].Value;
            query = query.Replace(tagMatch.Value, "").Trim();
        }

        // 构建 bool 查询
        var boolQuery = new BoolQuery
        {
            Must = new List<Query>
            {
                new MultiMatchQuery
                {
                    // 使用字符串隐式转换，避免访问 internal 构造函数
                    Fields = "title,content",
                    Query = query
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(tag))
        {
            boolQuery.Filter = new List<Query>
            {
                new TermQuery { Field = "tags", Value = tag }
            };
        }

        var searchRequest = new SearchRequest<ElasticQuestion>("questions")
        {
            Query = boolQuery
        };

        try
        {
            var response = await client.SearchAsync<ElasticQuestion>(searchRequest);
            // 使用 IsValidResponse（或 .Success）替代 IsValid
            if (!response.IsValidResponse)
                return Problem("Elasticsearch 搜索失败", response.DebugInformation);

            return Ok(response.Documents);
        }
        catch (Exception e)
        {
            return Problem("Elasticsearch 搜索失败", e.Message);
        }
    }

    // GET /elastic/similar-titles?query=xxx
    [HttpGet("similar-titles")]
    public async Task<IActionResult> SimilarTitles([FromQuery] string query)
    {
        var searchRequest = new SearchRequest<ElasticQuestion>("questions")
        {
            Query = new MatchQuery { Field = "title", Query = query }
        };

        try
        {
            var response = await client.SearchAsync<ElasticQuestion>(searchRequest);
            if (!response.IsValidResponse)
                return Problem("Elasticsearch 搜索失败", response.DebugInformation);

            return Ok(response.Documents);
        }
        catch (Exception e)
        {
            return Problem("Elasticsearch 搜索失败", e.Message);
        }
    }
}