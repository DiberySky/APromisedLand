using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace ElasticsearchService.Services;

public class ElasticsearchIndexSetup
{
    public static async Task EnsureIndexAsync(ElasticsearchClient client)
    {
        var indexName = "questions";

        var existsResponse = await client.Indices.ExistsAsync(indexName);
        if (existsResponse.Exists)
            return;

        // 只需定义映射，无需自定义 Analysis
        var mappings = new TypeMapping
        {
            Properties = new Properties
            {
                ["id"] = new KeywordProperty(),
                ["title"] = new TextProperty
                {
                    Analyzer = "ik_max_word",      // 索引时细粒度分词
                    SearchAnalyzer = "ik_smart"    // 搜索时粗粒度，提升精准度
                },
                ["content"] = new TextProperty
                {
                    Analyzer = "ik_max_word",
                    SearchAnalyzer = "ik_smart"
                },
                ["askerId"] = new KeywordProperty(),
                ["askerDisplayName"] = new TextProperty(),
                ["createdAt"] = new DateProperty(),
                ["updatedAt"] = new DateProperty(),
                ["viewCount"] = new IntegerNumberProperty(),
                ["tagSlugs"] = new KeywordProperty
                {
                    Fields = new Properties
                    {
                        ["keyword"] = new KeywordProperty() // 如需保留原始值，可选
                    }
                },
                ["hasAcceptedAnswer"] = new BooleanProperty(),
                ["votes"] = new IntegerNumberProperty(),
                ["answerCount"] = new IntegerNumberProperty()
            }
        };

        var createRequest = new CreateIndexRequest(indexName)
        {
            Mappings = mappings
            // Settings 可以完全省略，因为 IK 分析器已全局可用
        };

        var createResponse = await client.Indices.CreateAsync(createRequest);
        if (!createResponse.IsValidResponse)
        {
            throw new Exception($"创建索引失败: {createResponse.DebugInformation}");
        }
    }
}