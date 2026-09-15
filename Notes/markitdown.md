结合你之前关于 Weaviate 和中文 Embedding 的讨论，将 MarkItDown 设计为 **MAF (Microsoft Agent Framework) 工作流中的调用工具**，是当前架构下最自然、也最具扩展性的选择。下面从三种方案的核心差异出发，给出具体分析和建议。

---

### 三种方案核心对比

| 维度 | 独立微服务 | 与 Weaviate 集成 | MAF Workflow 工具 |
| :--- | :--- | :--- | :--- |
| **部署形态** | 独立进程/容器，HTTP API 暴露 | 转换逻辑嵌入数据摄取管道 | 作为 Agent 的可调用函数注册 |
| **调用方式** | REST / gRPC | 进程内方法调用 | `AIFunctionFactory.Create()` 或 `@tool` 装饰器 |
| **与 LLM 的关系** | 解耦，LLM 不感知转换过程 | 解耦，属于预处理阶段 | **LLM 自主决定何时调用**，转换是推理的一部分 |
| **扩展性** | 高，可被多语言/多系统复用 | 低，与特定管道绑定 | 高，可与其他工具组合成复杂工作流 |
| **MAF 原生支持** | 需通过 MCP 或自定义工具包装 | 需手动在管道中调用 | **原生一等公民**，直接注册为 Agent 工具 |
| **中文处理** | 取决于实现 | 取决于实现 | 可灵活选择本地中文模型或远程 API |

---

### 方案一：独立微服务

将 MarkItDown 封装为独立的 REST 或 MCP 服务，通过 HTTP 暴露转换能力。

**适用场景**：需要被多个技术栈（.NET、Python、Node.js）共享，或转换任务本身有独立的扩缩容需求。

**优势**：
- 技术栈无关，任何系统都可以通过 HTTP 调用
- 可独立扩缩容，适合高并发的批量文档转换场景
- 与 MAF 解耦，Agent 通过 **MCP 远程工具** 调用，符合 MAF 的远程工具集成范式

**劣势**：
- 引入网络延迟（每次转换 10-100ms+）和容器运维复杂度
- 需要额外的服务发现、健康检查和监控
- 对于 .NET 原生项目，增加了不必要的进程间通信开销

MarkItDown 官方已提供 MCP 服务器实现，可作为 Tier 3 层的独立服务接收 `convert_to_markdown(uri)` 调用。在 .NET 中，`Microsoft.Extensions.DataIngestion` 命名空间提供了 `MarkItDownMcpReader` 类，可直接连接远程 MCP 服务器。

---

### 方案二：与 Weaviate 集成（数据摄取管道的一部分）

将 MarkItDown 的转换逻辑嵌入从文档到 Weaviate 的数据摄取管道中，作为**预处理步骤**在分块和向量化之前执行。

**适用场景**：转换是纯粹的离线/批量预处理任务，不需要 LLM 参与决策，流程固定且可预测。

**优势**：
- 流程简单直接，无额外的网络跳转
- 与分块、Embedding、入库逻辑紧密耦合，便于统一管理

**劣势**：
- **转换与摄取强绑定**，无法被 Agent 在工作流中按需调用
- MAF 的 Agent 无法感知文档转换的存在，无法在对话过程中动态触发转换
- 如果后续需要支持新格式或切换转换引擎，需要修改整个摄取管道

这种方案适合**批处理式的 RAG 知识库构建**：一次性将大量文档转换、分块、向量化后存入 Weaviate，之后 Agent 只负责查询。但如果你的场景涉及**Agent 在对话中动态处理用户上传的文档**，这种设计会显得僵化。

---

### 方案三：MAF Workflow 的调用工具（推荐）

将 MarkItDown 注册为 MAF Agent 的**可调用工具**，让 LLM 自主决定何时调用转换函数。这是 MAF 的核心设计范式：工具是扩展 Agent 能力的可执行函数，模型根据任务上下文决定调用时机。

**适用场景**：Agent 需要在推理过程中动态处理文档，例如用户上传 PDF 后询问内容、Agent 自动抓取网页并转换为结构化文本等。

**实现方式**：

在 C# 中，使用 `AIFunctionFactory.Create()` 将任何 C# 方法直接转换为 MAF 工具，无需属性标注或基类继承：

```csharp
using Microsoft.Agents.AI;

// 定义转换工具方法
async Task<string> ConvertToMarkdown(string filePath)
{
    var markItDown = new MarkItDownClient();
    var result = await markItDown.ConvertAsync(filePath);
    return result.Markdown;
}

// 注册为 Agent 工具
var convertTool = AIFunctionFactory.Create(ConvertToMarkdown);

var agent = new ChatClientAgent(chatClient,
    instructions: "你可以将用户指定的文档转换为 Markdown 格式进行分析。",
    tools: new[] { convertTool });
```

当用户说“帮我分析这份 report.pdf 的核心结论”时，LLM 会自动识别需要调用 `ConvertToMarkdown` 工具，执行转换后将 Markdown 注入上下文进行推理。

**与 Weaviate 的协同**：

MAF 已内置对 **Weaviate 向量存储的支持**，可作为 Agent 的 **pluggable memory 后端**。这意味着你可以构建一个完整的 RAG 工作流：

1. **转换工具**：MarkItDown 将用户文档转为 Markdown
2. **检索工具**：通过 Weaviate 连接器执行语义搜索，召回相关分块
3. **推理**：LLM 基于检索结果和转换后的 Markdown 生成回答

三个工具在同一个 Agent 中协同工作，由 LLM 根据任务需要自主编排调用顺序。这正是 MAF 作为 **agentic AI 应用编排框架** 的核心价值。

---

### 选型建议

| 场景 | 推荐方案 | 理由 |
| :--- | :--- | :--- |
| **批量构建 RAG 知识库**，转换与查询分离 | 与 Weaviate 集成的摄取管道 | 流程简单，无需 Agent 参与预处理 |
| **多系统共享**转换能力，技术栈异构 | 独立微服务 / MCP Server | 跨语言复用，独立扩缩容 |
| **Agent 动态处理文档**，转换是推理的一部分 | **MAF Workflow 工具** | LLM 自主调用，与检索、推理无缝编排 |
| **既有批量摄取，又有 Agent 交互式处理** | **MAF 工具为主，微服务为辅** | 工具用于交互式场景，微服务用于批量管道 |

**综合建议**：以 **MAF Workflow 工具** 为主架构，将 MarkItDown 的转换能力直接暴露给 Agent 作为工具。如果未来需要跨系统共享或独立扩缩容，再将其抽取为 MCP Server 即可——MAF 对本地工具和远程 MCP 工具的支持是统一的，迁移成本较低。同时，利用 MAF 的 Weaviate 连接器将向量检索也注册为工具，形成“转换 → 检索 → 推理”的完整 Agent 工作流。