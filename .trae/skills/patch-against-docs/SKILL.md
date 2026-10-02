---
name: patch-against-docs
description: 把外部设计文档/补丁落地到本仓库前，逐项核对"文档假设"与"当前代码现状"，产出差异表。触发词："按设计文档落地补丁"、"对照文档核对待应用补丁"、"patch against docs check"。适用于 C#/Blazor/EF Core 项目。
---

# patch-against-docs

外部设计文档往往基于某个"理想"状态写成，与当前代码库常有偏差。直接在编译期或运行期才发现差异，代价高（一次可能触发 3–5 处编译错误或 1–2 个隐藏的边界缺陷）。

本技能在**编译之前**做 5 项核对，产出**可决策的差异表**。

## 何时使用

**触发条件**（任一）：
- 用户说"按设计文档落地补丁" / "对照文档核对待应用补丁"
- 用户粘贴了含代码块的设计文档，并要求"应用到项目中"
- 用户提供外部补丁文件（`.patch` / `.txt` / Markdown 代码块），需要落地
- 用户说"核对这份文档与实际代码"

**何时不用**：
- 纯文本说明、无具体代码补丁
- 只是咨询/讨论，不打算落地

## 输入

- **待应用的设计文档/补丁**（含代码块，通常是一批文件的完整内容或 diff）
- **当前代码库**（只读访问，用于核对，**不修改**）

## 执行流程

### 步骤 0：确定核对范围

从补丁中提取：
- 涉及的文件清单
- 每个文件引用的：DTO 属性、后端端点 URL、组件参数、测试选择器、using 命名空间

### 步骤 1–5：逐项核对

| # | 检查项 | 方法 | 若不符 |
|---|---|---|---|
| 1 | **DTO 字段** | 补丁引用的每个属性，在当前 DTO 定义中是否存在 | 记录：属性名 / 补丁假设 / 实际（含位置 `/path/File.cs:line`）/ 是否已存在等价字段 |
| 2 | **端点路径** | 补丁调用的 URL + Method，是否与后端 `[HttpGet]/[HttpPost]/...` 匹配 | 记录：调用点 / 后端定义 / 差异（路径/参数/Query String） |
| 3 | **组件参数** | 父组件传参 vs 子组件 `[Parameter]` 声明 | 记录：参数名 / 传入方 / 声明方 / 缺失或多余 |
| 4 | **按钮选择器** | bUnit 测试里的 `Find`/`FindAll` 定位是否稳健 | 检查是否依赖：SVG 内部字符串（如 `InnerHtml.Contains("Close")`）、易变 class 名、纯文本 | 建议替代：`[title='xxx']` / `[aria-label='xxx']` / `data-testid` |
| 5 | **using 依赖** | 新代码引用的命名空间是否已在文件顶部 / `_Imports.razor` 声明 | 记录：缺失的 using / 引用点 / 应加在哪 |

### 步骤 6：产出差异表

```
## 补丁核对差异表

| # | 项 | 文档假设 | 实际情况 | 处理建议 |
|---|----|---------|---------|---------|
| 1 | DTO 字段 | `CustomTableColumnDto.IsDeleted` 不存在 | 存在于 `MetadataDtos.cs:162` | 保留文档用法，删除多余的本地判断 |
| 2 | ... | | | |

## 应用建议

- **可直接应用**：文件 A / B
- **需调整**：文件 C（差异点 #1、#3）
- **需回滚**：文件 D（差异点 #2）
- **额外发现**：文件 X 存在缺陷 Y（非本次补丁引入），建议一并处理
```

### 步骤 7：交回决策

**不静默适配**。差异出现时，必须由用户决定：
- 改文档（当前代码是权威）
- 改代码（文档设计更合理）
- 暂缓（先做其它差异）

## 输出原则

1. **每条差异都要有证据**（文件路径 + 行号 + 代码片段）
2. **区分"补丁引入的差异"与"补丁暴露的既有缺陷"**——后者往往更值钱
3. **不假设用户偏好**：改文档/改代码是产品决策，不是技术决策
4. **建议可执行**：每条差异末尾给出具体动作（"保留 X 用法" / "回滚 Y 变更" / "补充 Z 定义"）

## Example

### 输入

设计文档含一段 `CustomTableEditor.razor` 的完整替换，内部有 3 处：
```razor
foreach (var col in _tableDef.Columns.Where(c => !c.IsDeleted))
```

用户提示："按设计文档落地补丁"。

### 执行

**步骤 1（DTO 字段）**：
- 扫描 `TreeGraph.Shared/Eav/Dtos/MetadataDtos.cs`
- 找到 `CustomTableColumnDto` 定义，确认 `IsDeleted` 字段**存在**（line 162）
- 结论：补丁用法**合法**

**步骤 3（组件参数）**：
- 扫描 `CustomTableEditor.razor` 的 `[Parameter]`
- 与 `EntityEdit.razor` 的调用点比对
- 6 个参数全部匹配

**步骤 4（按钮选择器）**：
- 发现补丁的 bUnit 测试用 `InnerHtml.Contains("Close")` 定位移除按钮
- MudBlazor 9.x 渲染 SVG，`Close` 字符串可能不在 DOM 中
- 建议改用 `title='移除'`

### 输出差异表

| # | 项 | 文档假设 | 实际情况 | 处理建议 |
|---|----|---------|---------|---------|
| 1 | DTO 字段 | `IsDeleted` 不存在 | 存在 `MetadataDtos.cs:162` | **保留**文档用法 |
| 2 | 按钮选择器 | `InnerHtml.Contains("Close")` | MudBlazor 9.x 无此字符串 | 改为 `[title='移除']` |

### 附带发现

在核对步骤 1 时发现：**文档补丁声称"渲染层不要 IsDeleted 过滤"，与后端 `ToCustomTableDto` 不过滤已删除列的现状冲突**。

→ 若照文档落地，会引入**真实缺陷**：数据编辑器显示已删除列 + 保存时回传旧值 → 后端 400。

**建议加一个测试锁定契约**：`LoadTable_ExcludesDeletedColumns`，断言已删除列不出现。

## 反例（不要这样做）

- ❌ 只跑 `dotnet build`，等编译错误再改
- ❌ 发现差异后自动"修正"文档里的代码（改代码是用户的决策）
- ❌ 差异表只写"不匹配"，不写具体位置和替代方案
- ❌ 忽略"补丁暴露的既有缺陷"——这恰恰是最高价值产出

## 附：手动 fallback 命令

无技能环境时，至少跑一遍：

```powershell
# 1. DTO 字段核对：搜属性名
Select-String -Path "TreeGraph.Shared\Eav\Dtos\*.cs" -Pattern "IsDeleted"

# 2. 端点路径核对：搜控制器里的路由
Select-String -Path "TreeGraph.Api\Controllers\*.cs" -Pattern "\[Http"

# 3. 组件参数核对：列出所有 [Parameter]
Select-String -Path "TreeGraph.Blazor\Components\**\*.razor" -Pattern "\[Parameter"

# 4. 编译（最后再跑）
dotnet build
```
