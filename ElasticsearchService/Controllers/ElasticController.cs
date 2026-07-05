using System.Text.RegularExpressions;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ElasticsearchService.Embeds;
using ElasticsearchService.Models;
using Microsoft.AspNetCore.Mvc;

namespace ElasticsearchService.Controllers;

[ApiController]
[Route("[controller]")]
public class ElasticController(ElasticsearchClient client, IEmbeddingService embeddingService) : ControllerBase
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
        // var boolQuery = new BoolQuery
        // {
        //     Must = new List<Query>
        //     {
        //         new MultiMatchQuery
        //         {
        //             // 使用字符串隐式转换，避免访问 internal 构造函数
        //             Fields = "title,content",
        //             Query = query
        //         }
        //     }
        // };

        var boolQuery = new BoolQuery
        {
            Must = new List<Query>
            {
                new MultiMatchQuery
                {
                    Fields = new[] { "title^3", "content" }, // 标题权重更高
                    Query = query,
                    Analyzer = "ik_smart" // 使用 ik_smart 进行搜索
                    // 或 "ik_max_word"，视需求而定
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
            Query = new MatchQuery
            {
                Field = "title",
                Query = query,
                Analyzer = "ik_smart"
            }
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

    [HttpGet("semantic")]
    public async Task<IActionResult> SemanticSearch([FromQuery] string query, [FromQuery] int size = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("查询词不能为空");

        // 1. 生成查询向量
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query);

        // 2. 构建 knn 搜索请求（Elastic 8.x 支持 knn 查询）
        var searchRequest = new SearchRequest<ElasticQuestion>("questions")
        {
            Size = size,
            Knn =
            [
                new KnnSearch()
                {
                    Field = "embedding",
                    QueryVector = queryEmbedding,
                    K = size * 2, // 召回更多候选（用于后续 rerank，若需要）
                    NumCandidates = size * 10,
                    Boost = (float?)0.5 // 控制向量得分权重
                },
            ]
        };

        try
        {
            var response = await client.SearchAsync<ElasticQuestion>(searchRequest);
            if (!response.IsValidResponse)
                return Problem("语义搜索失败", response.DebugInformation);

            // 返回匹配的文档（已包含相关度得分）
            return Ok(response.Documents);
        }
        catch (Exception ex)
        {
            return Problem("语义搜索异常", ex.Message);
        }
    }
}