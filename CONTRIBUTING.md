# Contributing to APromisedLand

## 应用外部设计补丁前的核对清单

外部设计文档（含代码补丁）常基于某个"理想"状态，与当前代码库存在偏差。**在编译之前**做 5 项核对，可避免编译期才发现契约不符，更能捕捉运行期才暴露的边界缺陷。

### 5 项核对

| # | 检查项 | 方法 | 举例 |
|---|--------|------|------|
| 1 | **DTO 字段** | 补丁引用的每个属性，在当前 DTO 定义中是否存在 | `CustomTableColumnDto.IsDeleted` |
| 2 | **端点路径** | 客户端调用的 URL + Method，是否与后端 `[Http...]` 匹配 | `GET /option-sets/{id}?includeDeleted` |
| 3 | **组件参数** | 父组件传参 vs 子组件 `[Parameter]` 声明 | `CustomTableEditor` 的 6 个参数 |
| 4 | **按钮选择器** | bUnit 测试定位是否稳健（不依赖 SVG 内部字符串 / 易变 class） | 用 `[title='移除']` 而非 `InnerHtml.Contains("Close")` |
| 5 | **using 依赖** | 新代码引用的命名空间是否已声明 | `Microsoft.AspNetCore.Components.Rendering` |

### 手动 fallback 命令

无自动化环境时，逐个跑：

```powershell
# 1. DTO 字段
Select-String -Path "TreeGraph.Shared\Eav\Dtos\*.cs" -Pattern "<属性名>"

# 2. 端点路由
Select-String -Path "TreeGraph.Api\Controllers\*.cs" -Pattern "\[Http"

# 3. 组件参数
Select-String -Path "TreeGraph.Blazor\Components\**\*.razor" -Pattern "\[Parameter"

# 4. 编译（放最后）
dotnet build
```

### 推荐流程

Claude Code 环境：使用工作区技能 **`patch-against-docs`**（触发词："按设计文档落地补丁"），自动执行 5 项核对并产出差异表。

### 核心理念

- **不静默适配**：差异出现时，由用户决定改文档还是改代码
- **区分两类差异**：
  - **补丁引入的差异**（编译错误、契约不符）→ 修
  - **补丁暴露的既有缺陷**（如边界条件下的空值处理）→ **价值更高，优先修**
- **差异表格式**：`项 / 文档假设 / 实际情况 / 处理建议`，每条附文件路径 + 行号

---

## 案例：一次误判催生两个真实缺陷

**背景**：应用一份 `CustomTableEditor.razor` 的完整替换补丁时，核对者先入为主地断言"`CustomTableColumnDto` 没有 `IsDeleted`"，建议删除 3 处 `.Where(c => !c.IsDeleted)`。

**实际**：`IsDeleted` 存在于 `MetadataDtos.cs:162`。**该删除建议是错的**。

**如果没有这次误判的收敛**：

假如照着"删除过滤"落地，会引入两类缺陷（后续审查才发现）：
- **渲染层缺陷**：数据编辑器渲染已删除列的输入框 → 用户填值 → 保存 → 后端 400
- **数据层缺陷**：加载含已删除列历史数据的行时，`Fields` 保留旧值 → 保存时回传 → 后端 400

**最终产出**：
- 一个 bUnit 测试 `LoadTable_ExcludesDeletedColumns` 锁定"数据编辑器不显示已删除列"的契约
- 一个语义分工的明确：**管理页（`CustomTables.razor`）展示已删除列 + 提供恢复入口；数据编辑器（`CustomTableEditor`）不显示、不校验、不回传已删除列**

**教训**：

> 核对者断言"XX 不存在"时，**必须给出证据**（`Select-String` 结果 / 文件行号）。凭记忆或直觉断言是误判的高发源头。而一旦发现误判，不要只看"删掉错误建议就行"，而要问："如果错误建议被采纳，会漏掉什么？"——那里往往藏着真正的缺陷。

---

## 编译与测试基线

本仓库当前的绿色基线（提交前请核对）：

| 项目 | 编译 | 测试 |
|---|---|---|
| `TreeGraph.Api` | 0 警告 0 错误 | — |
| `TreeGraph.Api.Tests` | 0 警告 0 错误 | 23/23 |
| `TreeGraph.Blazor` | 0 警告 0 错误 | — |
| `TreeGraph.Blazor.Tests` | 0 警告 0 错误 | 79/79 |

提交前跑：

```powershell
dotnet build
dotnet test
```
