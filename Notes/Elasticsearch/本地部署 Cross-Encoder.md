将重排序服务替换为 Cross-Encoder 模型，有几种主流方案，主要可以归为**本地部署**和**托管 API** 两类。下面是它们的对比和实现方式。

### 方案对比

#### **本地部署 Cross-Encoder**
*   **优点**：无 API 调用费用；数据不出本地，隐私安全；可离线运行。
*   **缺点**：需要自行维护模型与计算资源；CPU 推理较慢，GPU 能提升速度但会增加成本。
*   **适用场景**：对数据隐私要求高、查询量巨大或希望长期控制成本。

#### **托管 API（如 Cohere, Elastic Rerank）**
*   **优点**：开箱即用，无需维护基础设施；模型精度高，持续更新；按量付费，初期成本低。
*   **缺点**：长期大规模使用成本可能较高；依赖外部服务与网络。
*   **适用场景**：希望快速上线、查询量中等或不想投入运维精力。

---

### 方案一：本地部署 (以 `LMSupply.Reranker` 为例)

`LMSupply.Reranker` 是一个轻量级 .NET 库，支持自动下载和运行 Hugging Face 上的 Cross-Encoder 模型。

**1. 安装 NuGet 包**

```bash
dotnet add package LMSupply.Reranker
```

**2. 实现 `IRerankService`**

```csharp
using ElasticsearchService.Models;
using LMSupply.Reranker;

public class LocalCrossEncoderReranker : IRerankService
{
    // 选择一个合适的模型，例如 BAAI/bge-reranker-base
    private const string ModelName = "BAAI/bge-reranker-base";
    private readonly CrossEncoder _reranker;

    public LocalCrossEncoderReranker()
    {
        // 初始化 CrossEncoder，useGpu: true 可启用 GPU 加速
        _reranker = new CrossEncoder(ModelName, useGpu: false);
    }

    public async Task<List<ElasticQuestion>> RerankAsync(string query, List<ElasticQuestion> documents, int topN)
    {
        if (!documents.Any()) return new List<ElasticQuestion>();

        // 1. 构造 (query, document) 对
        var pairs = documents.Select(doc => new[]
        {
            query,
            $"{doc.Title} {doc.Content}" // 将标题和内容拼接
        }).ToList();

        // 2. 获取 Cross-Encoder 相关性分数
        var scores = await _reranker.PredictAsync(pairs);

        // 3. 按分数降序排序，取前 topN 个
        var reranked = documents
            .Select((doc, index) => new { doc, score = scores[index] })
            .OrderByDescending(x => x.score)
            .Take(topN)
            .Select(x => x.doc)
            .ToList();

        return reranked;
    }
}
```

**3. 在 `Program.cs` 中注册**

```csharp
builder.Services.AddSingleton<IRerankService, LocalCrossEncoderReranker>();
```

---

### 方案二：使用 Cohere Rerank API

Cohere 提供了专门的重排序端点，使用方便，效果出色。

**1. 安装 NuGet 包**

```bash
dotnet add package CohereDotnet
```

**2. 实现 `IRerankService`**

```csharp
using Cohere;
using Cohere.Types.Rerank;
using ElasticsearchService.Models;

public class CohereRerankService : IRerankService
{
    private readonly CohereClient _cohereClient;

    public CohereRerankService(IConfiguration config)
    {
        var apiKey = config["Cohere:ApiKey"] 
            ?? throw new InvalidOperationException("Cohere API Key is missing.");
        _cohereClient = new CohereClient(apiKey); // 使用 Cohere.NET SDK
    }

    public async Task<List<ElasticQuestion>> RerankAsync(string query, List<ElasticQuestion> documents, int topN)
    {
        if (!documents.Any()) return new List<ElasticQuestion>();

        // 1. 准备文档列表
        var docs = documents.Select(d => new RerankDocument(d.Id, $"{d.Title} {d.Content}")).ToList();

        // 2. 调用 Rerank API
        var rerankRequest = new RerankRequest
        {
            Query = query,
            Documents = docs,
            TopN = topN,
            Model = "rerank-english-v3.0" // 或 rerank-multilingual-v3.0
        };

        var response = await _cohereClient.RerankAsync(rerankRequest);

        // 3. 按返回的索引重新排序
        var reranked = response.Results
            .OrderBy(r => r.Index)
            .Select(r => documents[r.Index])
            .ToList();

        return reranked;
    }
}
```

**3. 在 `Program.cs` 中注册**

```csharp
builder.Services.AddSingleton<IRerankService, CohereRerankService>();
```

---

### 方案三：使用 Elastic Rerank (官方模型)

如果你的 Elasticsearch 是 8.17+ 版本且有合适的订阅，可以使用 Elastic 官方训练的 Cross-Encoder 模型。

**1. 部署模型 (在 Kibana Dev Console 中执行)**

```json
PUT _inference/rerank/elastic-rerank-v1
{
  "service": "elasticsearch",
  "service_settings": {
    "model_id": ".rerank-v1"
  }
}
```


**2. 实现 `IRerankService`**

```csharp
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Inference;
using ElasticsearchService.Models;

public class ElasticRerankService : IRerankService
{
    private readonly ElasticsearchClient _client;
    private const string InferenceId = "elastic-rerank-v1";

    public ElasticRerankService(ElasticsearchClient client)
    {
        _client = client;
    }

    public async Task<List<ElasticQuestion>> RerankAsync(string query, List<ElasticQuestion> documents, int topN)
    {
        if (!documents.Any()) return new List<ElasticQuestion>();

        // 1. 构建 rerank 请求
        var rerankRequest = new RerankRequest(InferenceId)
        {
            Query = query,
            Documents = documents.Select(d => new RerankDocument
            {
                Id = d.Id,
                Text = $"{d.Title} {d.Content}"
            }).ToList(),
            TopN = topN
        };

        // 2. 调用 Inference API 进行重排序
        var response = await _client.Inference.RerankAsync(rerankRequest);

        // 3. 按返回结果排序
        var reranked = response.Results
            .OrderBy(r => r.Index)
            .Select(r => documents[r.Index])
            .ToList();

        return reranked;
    }
}
```

**3. 在 `Program.cs` 中注册**

```csharp
builder.Services.AddSingleton<IRerankService, ElasticRerankService>();
```

---

### 其他优秀的开源模型（供本地部署参考）

如果不使用 `LMSupply.Reranker`，也可以通过 `sentence-transformers` (Python) 或 ONNX Runtime (C#) 部署以下模型：

| 模型 | 特点 | 许可 |
| :--- | :--- | :--- |
| **BAAI/bge-reranker-base** | 中英文效果好，性能均衡 | MIT |
| **BAAI/bge-reranker-v2-m3** | 多语言模型，支持100+语言 | MIT |
| **cross-encoder/ms-marco-MiniLM-L-6-v2** | 经典轻量级模型 | Apache 2.0 |
| **zeroentropy/zerank-1-small** | 在多个领域表现优异 | 需查阅 |

### 总结与建议

1.  **快速验证**：推荐 **Cohere API**，无需搭建环境，集成简单，效果有保障。
2.  **长期/大规模使用，追求成本最优**：推荐 **本地部署** (`LMSupply.Reranker`)，无API费用，但需规划好计算资源。
3.  **深度绑定 Elastic Stack**：如果已在使用 Elastic Cloud 且版本符合要求，**Elastic Rerank** 是最便捷的选择。

你可以在 `Program.cs` 中通过替换注册的服务，轻松在这几种方案间进行切换和对比测试。