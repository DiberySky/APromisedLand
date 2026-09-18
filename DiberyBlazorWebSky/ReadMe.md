

# 新增 `GraphChat.razor`，保留原 `Chat.razor` 不变

你的 `GraphChat.razor` 代码基本对，只有一处需要修正：`@inject ILogger<Chat> Logger` → `@inject ILogger<GraphChat> Logger`（当前引用了错误的类型，可能编译警告或运行时不一致）。

另外为了命名风格统一（现有页面是 `/chat`、`/graph`、`/writer-critic` 小写连字符），建议路由改为 `/graph-chat`。

---

## 1. 修正 `Components/Pages/GraphChat.razor`（头部）

**只改前 7 行**：

```razor
@page "/graph-chat"
@using DiberyBlazorWebSky.Models.Graph
@inject ChatApiClient ChatApi
@inject GraphApiClient GraphApi
@inject IJSRuntime JS
@inject ILogger<GraphChat> Logger
@implements IDisposable
@rendermode InteractiveServer

<PageTitle>图对话</PageTitle>
```

**改动点**：
- `@page "/GraphChat"` → `@page "/graph-chat"`（统一小写连字符）
- `ILogger<Chat>` → `ILogger<GraphChat>`
- `<PageTitle>` 改为"图对话"（区分于普通 Chat）

**其余代码**你贴的全部保留，无需修改。

---

## 2. 创建 `Components/Pages/GraphChat.razor.css`

**从 `Chat.razor.css` 复制全部内容**（CSS isolation 会为两个页面生成独立 scope ID，同样的类名不会冲突），然后**在末尾追加**图上下文工具栏样式：

```css
/* ══════════════════════════════════════════════════════ */
/* 图上下文工具栏（GraphChat 特有）                      */
/* ══════════════════════════════════════════════════════ */

.graph-context-bar {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 16px;
    background: #f8fafc;
    border-bottom: 1px solid #e2e8f0;
    flex-wrap: wrap;
}

.graph-toggle {
    display: flex;
    align-items: center;
    gap: 6px;
    cursor: pointer;
    font-size: 0.88rem;
    color: #334155;
    user-select: none;
}

.graph-toggle input[type="checkbox"] {
    width: 16px;
    height: 16px;
    cursor: pointer;
    accent-color: #4a90e2;
}

.graph-selector {
    padding: 6px 10px;
    font-size: 0.85rem;
    border: 1px solid #cbd5e1;
    border-radius: 6px;
    outline: none;
    background: #fff;
    min-width: 200px;
    max-width: 360px;
}

.graph-selector:disabled {
    background: #f1f5f9;
    color: #94a3b8;
    cursor: not-allowed;
}

.graph-context-info {
    font-size: 0.78rem;
    color: #0ea5e9;
    background: #e0f2fe;
    padding: 3px 10px;
    border-radius: 12px;
    font-weight: 500;
}

.message-context-badge {
    display: inline-block;
    margin-top: 6px;
    padding: 2px 8px;
    background: #e0f2fe;
    color: #0369a1;
    border-radius: 10px;
    font-size: 0.72rem;
    font-weight: 500;
}

.context-hint {
    margin-top: 16px;
    font-size: 0.85rem;
    color: #0284c7;
    background: #f0f9ff;
    padding: 10px 16px;
    border-radius: 8px;
    display: inline-block;
}
```

---

## 3. 确认 `Models/Graph/GraphContextBuilder.cs` 存在

如果没有，创建它：

```csharp
using System.Text;

namespace DiberyBlazorWebSky.Models.Graph;

/// <summary>
/// 把图数据序列化为 LLM 友好的文本，用于 GraphChat 页面
/// 将图上下文注入用户消息。
/// </summary>
public static class GraphContextBuilder
{
    /// <summary>超过此节点数则不附加图上下文。</summary>
    public const int MaxNodeCount = 80;

    public static string Build(
        string graphName,
        IReadOnlyList<NodeDto> nodes,
        IReadOnlyList<EdgeDto> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\">");

        sb.AppendLine("<nodes>");
        if (nodes.Count == 0)
        {
            sb.AppendLine("(无节点)");
        }
        else
        {
            foreach (var n in nodes)
            {
                var labels = n.Labels is { Count: > 0 }
                    ? $" [{string.Join(", ", n.Labels)}]"
                    : "";
                sb.AppendLine($"- {n.Name}{labels} (GUID: {n.Guid})");
            }
        }
        sb.AppendLine("</nodes>");

        sb.AppendLine("<edges>");
        if (edges.Count == 0)
        {
            sb.AppendLine("(无边)");
        }
        else
        {
            var nodeLookup = nodes.ToDictionary(x => x.Guid, x => x.Name);
            foreach (var e in edges)
            {
                var fromName = nodeLookup.TryGetValue(e.From, out var f) ? f : e.From.ToString();
                var toName   = nodeLookup.TryGetValue(e.To, out var t)   ? t : e.To.ToString();
                sb.AppendLine($"- {fromName} --[{e.Name}]--> {toName}");
            }
        }
        sb.AppendLine("</edges>");

        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    public static string BuildPrompt(
        string graphName,
        IReadOnlyList<NodeDto> nodes,
        IReadOnlyList<EdgeDto> edges,
        string userMessage)
    {
        var context = Build(graphName, nodes, edges);

        return $"""
以下是当前图的上下文。请基于这些真实数据回答用户问题。
如果上下文中不包含答案，请明确说明，不要编造。

{context}

用户问题：{userMessage}
""";
    }
}
```

---

## 4. 更新 `Components/Layout/NavMenu.razor`

在现有 `Chat` 和 `Writer-Critic` 之间插入：

```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="chat">
        <span class="bi bi-chat-dots-nav-menu" aria-hidden="true"></span> Chat
    </NavLink>
</div>

<!-- ★ 新增 -->
<div class="nav-item px-3">
    <NavLink class="nav-link" href="graph-chat">
        <span class="bi bi-diagram-3-fill-nav-menu" aria-hidden="true"></span> 图对话
    </NavLink>
</div>

<div class="nav-item px-3">
    <NavLink class="nav-link" href="writer-critic">
        <span class="bi bi-pencil-square-nav-menu" aria-hidden="true"></span> Writer-Critic
    </NavLink>
</div>
```

---

## 5. 最终文件清单

| 文件 | 操作 |
|---|---|
| `Components/Pages/Chat.razor` | **不动**（保留原版） |
| `Components/Pages/Chat.razor.css` | **不动** |
| `Components/Pages/GraphChat.razor` | **新增**（已存在，改头部 3 处） |
| `Components/Pages/GraphChat.razor.css` | **新增**（复制 Chat 的 CSS + 追加图上下文样式） |
| `Models/Graph/GraphChatMessage.cs` | ✅ 已存在 |
| `Models/Graph/GraphContextBuilder.cs` | **确认存在**（没有就创建） |
| `Components/Layout/NavMenu.razor` | **修改**（加导航项） |

---

## 6. 编译 + 验证

```powershell
cd D:\APromisedLand\DiberyBlazorWebSky
dotnet build
```

重启 AppHost，打开 Blazor。

### 验证流程

1. **左侧导航栏**应出现两个入口：
    - **`Chat`** —— 普通聊天，无图上下文
    - **`图对话`** —— 新增，带图上下文工具栏

2. 点 **`图对话`**：
    - 页面顶部出现**图上下文工具栏**：`☑ 使用图上下文` + 图下拉框
    - 图表从 `MAFWorkFlowApi` 加载（应该看到 `TestGraph`、`SecondGraph` 等）

3. **勾选"使用图上下文"** → 选择 `TestGraph`：
    - 右侧应显示 `已加载：3 节点 / 1 边`（浅蓝徽章）

4. 输入：`根节点C 和 根节点B 是什么关系？`
5. 发送

### 预期效果

**用户消息气泡**：
```
根节点C 和 根节点B 是什么关系？
📊 TestGraph（3 节点 / 1 边）
13:45:23
```

**AI 回答**（基于真实图数据）：
```
根节点C 通过 PARENT_OF 边指向根节点B，因此根节点C 是根节点B 的父节点。
```

**关键**：回答里出现**真实节点名**（不是 "Node A" / "Node B"），说明图上下文注入成功。

---

## 7. 可选：加一段"没启用图上下文"的提示

如果用户**不勾**"使用图上下文"，页面就退化为普通聊天。建议给用户一点视觉提示——**在页头加一行**：

```razor
<PageTitle>图对话</PageTitle>

<div class="chat-container">
    <div class="chat-header">
        <h1>🤖 图对话助手</h1>
        <p class="page-subtitle">
            @if (useGraphContext && !string.IsNullOrEmpty(selectedGraphGuid))
            {
                <span>已启用图上下文 —— 回答将基于所选图的真实数据</span>
            }
            else
            {
                <span>提示：勾选"使用图上下文"可让 AI 基于图数据回答</span>
            }
        </p>
        ...
```

CSS：

```css
.page-subtitle {
    margin: 4px 0 0 0;
    font-size: 0.85rem;
    color: #64748b;
}
```

**可选**——非必需。

---

## 请回报

1. **`dotnet build`** 是否成功
2. **侧边栏是否出现"图对话"入口**
3. **勾选图上下文 → 提问 → AI 回答是否引用真实节点名**（截图）

如果回答是"我不知道有根节点C"之类——把 **MAFWorkFlowApi Console 里 Chat 请求的日志**发我，我看看图上下文拼接的 prompt 是否被服务端接收。

**这一步做完，你的 GraphRAG 前端就完整了：普通聊天 + 图增强对话 + 图可视化浏览三件套。**