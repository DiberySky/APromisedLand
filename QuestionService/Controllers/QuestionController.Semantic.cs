using System.Text.Json;
using APromisedLand.Shared.MessageContracts;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.MachineLearning;
using Microsoft.AspNetCore.Mvc;

namespace QuestionService.Controllers;

public partial class QuestionsController
{
    [HttpGet("semantic-search")]
    public async Task<ActionResult<List<QuestionSearchResult>>> SemanticSearch([FromQuery] string q, [FromQuery] int size = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("查询词不能为空");

        // 生成查询向量
        var queryVector = await embedder.GenerateAsync(q);
    
        var searchRequest = new SearchRequest<ElasticQuestion>("questions")
        {
            Size = size,
            Query = new KnnQuery
            {
                Field = "titleVector",       // 可同时搜索 titleVector 和 contentVector，使用多个 knn 或 script_score
                QueryVector = queryVector,
                K = size * 2,
                NumCandidates = 100
            }
            // 可与 bool 查询结合做混合搜索（参考 Elasticsearch 8.x 的 knn + query）
        };

        try
        {
            var response = await elasticClient.SearchAsync<ElasticQuestion>(searchRequest);
            if (!response.IsValidResponse)
                return Problem("语义搜索失败", response.DebugInformation);

            var scroes = response.Hits.Select(x => x.Score).ToList();
            
            // return Ok(response.Documents);
            
            return response.Hits
                .Select(hit => new QuestionSearchResult
                {
                    Id = hit.Source.Id,
                    Title = hit.Source.Title,
                    Content = hit.Source.Content,
                    Score = (float)(hit.Score ?? 0)
                })
                .ToList();
        }
        catch (Exception ex)
        {
            return Problem("语义搜索异常", ex.Message);
        }
    }
    
    [HttpGet("doc/{id}")]
    public async Task<IActionResult> GetQuestionDoc(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("文档 ID 不能为空");

        try
        {
            var response = await elasticClient.GetAsync<ElasticQuestion>(id, 
                idx => idx.Index("questions"));

            if (!response.IsValidResponse)
            {
                if (response.ApiCallDetails?.HttpStatusCode == 404)
                    return NotFound($"未找到 ID 为 {id} 的文档");
                return Problem("检索失败", response.DebugInformation);
            }

            var json = JsonSerializer.Serialize(response.Source);
            logger.LogInformation("从ES获取的文档: {Json}", json);
            
            // 若文档存在，response.Source 即为 ElasticQuestion 对象
            return Ok(response.Source);
        }
        catch (Exception ex)
        {
            return Problem("检索异常", ex.Message);
        }
    }
}