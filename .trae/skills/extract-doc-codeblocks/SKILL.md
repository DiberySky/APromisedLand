---
name: extract-doc-codeblocks
description: 把中文设计文档 txt 的 fenced 代码块逐字提取覆盖到项目文件并做字节比对，含按「文件 N/M」与按「中文数字/N.N 章节」两种模式及本仓库固定适配规则。用户给出 long-text 文档路径要求按文档实施/替换/补齐代码时使用；手写新代码、局部小补丁、运行时调试时不要使用。
---

# 提取设计文档代码块并落盘（含本仓库适配规则）

把"长文本设计文档 → 项目文件"的机械映射固化为脚本。文档是唯一事实源，
提取过程**字节级保真**，禁止任何智能改写；仅允许在提取后应用本仓库已知的
固定适配（见第 5 步），且必须在报告中列出。

## 触发场景

用户消息给出一个 `.txt` 文档路径（通常在
`C:\Users\_Dibery_\AppData\Roaming\Trae CN\User\workspaceStorage\...\long-text\<session-id>\<doc-id>\`
下，文件名常以 `#` 开头并含空格），要求"按文档实施/替换现有文件/补齐"。

## 1. 解析真实文档路径

- 用户消息常带拼接伪前缀，形如 `d:\APromisedLand/c:\Users\...\# xxx.txt`，
  真实路径是 `c:\Users\...` 部分。
- 路径含 `#`/中文冒号/空格时，不要手写路径传参；先用 PowerShell 解析：

  ```powershell
  $doc = (Get-ChildItem "<doc-id 目录>\*.txt").FullName
  ```

- 先扫章节标题与 fenced 块分布，确认文件清单：用 Python 打印
  `^#{1,4} ` 行与 ``` 围栏行号（文件名为 `#` 开头时 Read 工具可能失败，
  一律用 Python 读）。

## 2. 判断文档格式，选脚本

| 格式 | 标题样例 | 脚本 |
|---|---|---|
| A | `## 文件 1/6：\`相对路径\`` | `scripts/extract_codeblocks.py`（按序号，自动取标题内路径） |
| B | `## 三、\`Name.razor\`` 或 `## 1.1 \`X.cs\`（改造）`，路径写在正文「路径：」行，一节可有多块 | `scripts/extract_by_section.py`（按 `章节键|块序号|目标路径`） |

格式 B 典型命令：

```powershell
python "<技能目录>\scripts\extract_by_section.py" $doc --root "d:\APromisedLand" `
  --map "三|0|TreeGraph.Blazor\wwwroot\js\viewport.js" `
  --map "六|1|TreeGraph.Blazor.Shared\Responsive\ResponsiveSplit.razor.css" --dry-run
```

- 章节键：中文数字（`三`）或 `1.1`；块序号 0 基（含 ### 子节内的块，按全文顺序）。
- 先 `--dry-run` 核对字节数与章节/块数量，再正式落盘。
- 两脚本均要求每个文件输出 `[OK]`；`[MISMATCH]`/`[ERROR]` 必须停下排查。
- PowerShell 不支持 bash heredoc；任何临时 Python 一律用
  `@'...'@ | python3 -`（分隔符独占一行）。优先直接调用固化脚本，不要内联重写。

## 3. 整文件替换 vs 手工补丁

- 标题后紧跟一个完整 fenced 块 = **整文件替换**，用脚本。
- 文档写"追加/仅补丁/改造片段/不要整文件覆盖"，或代码块只是局部方法：
  **跳过脚本**，Read 目标区域真实内容后用 Edit 最小锚点插入。
- 文档中"验证清单/确认问题/下一步/约束遵守"等章节不提取。

## 4. 落盘保真红线

- 写盘逐字节（格式 B 脚本用 `write_bytes`，等价 `newline=""`）：
  不合并换行、不清理标记、不规范化标点/引号/空格、不"顺手优化"。
- razor 中文注释/文案保留 UTF-8 原文。
- 不在仓库留一次性脚本；脚本只放本技能 `scripts/`。

## 5. 提取后的固定适配（本仓库 d:\APromisedLand 专用）

这些是已反复验证的环境差异，提取后、构建前主动检查并最小修正，
最终逐条列入偏差报告：

1. **MudBlazor 9.11.0**：`IDialogService.ShowMessageBox(...)` 已不存在，
   全部改 `ShowMessageBoxAsync(...)`（参数签名兼容，仅加 Async 后缀）。
2. **事件 stopPropagation 只能挂 DOM 元素**：文档常把
   `OnClick:stopPropagation="true"` 和 `$event` 写在 `MudIconButton` 等
   **组件参数**上（Razor 会代码生成错乱，报 CS0115/CS0065/CS1026）。
   改为外层包一个 `<div/span @onclick:stopPropagation="true">`，
   组件上只留 `OnClick`；对应 handler 去掉未使用的 `MouseEventArgs` 形参。
3. **Blazor 宿主页面目录**：文档写 `TreeGraph.Blazor/Pages/`、
   `Pages/Desktop/` 等，实际统一落 `TreeGraph.Blazor\Components\Pages\`
   （.NET 8+ Blazor Web App），平铺免建子目录。
4. **RCL 静态资源**：`TreeGraph.Blazor.Shared\wwwroot\...` 自动映射
   `_content/TreeGraph.Blazor.Shared/...`，App.razor 引用用该前缀。
5. **属性内双引号**：`OnClick="() => Nav.NavigateTo("/x")"` 会 CS1026，
   抽成 `@code` 里的方法再绑定方法组。
6. **缺 using**：`NavLinkMatch` 需要 `@using Microsoft.AspNetCore.Components.Routing`
   （RCL 组件内不依赖宿主 _Imports）。
7. **DI 手工注册模式**：宿主 Program.cs 对 RCL 类型化客户端采用手工
   `AddHttpClient<TInterface,TImpl>(BaseAddress=https+http://treegrapheavapi)
   .AddStandardResilienceHandler(NonIdempotentResilience.Configure)`，
   不盲抄文档里的工厂/双注册写法。

## 6. 构建、迁移与回归

- 受影响项目逐个 `dotnet build <proj> -c Debug --nologo -v q`，必须 0 错误。
- RCL 回归：`dotnet test TreeGraph.Blazor.Shared.Tests/TreeGraph.Blazor.Shared.Tests.csproj -c Debug --nologo`
  （基线 240/240，不得下降）。
- 实体变更需要 EF 迁移时：**只生成不应用**，迁移目录 `TreeGraph.Api\Data\Migrations`：

  ```powershell
  dotnet ef migrations add <Name> --project TreeGraph.Api `
    --context EavDbContext -o Data/Migrations
  ```

  生成后 Read 迁移确认只含预期变更（无意外模型漂移）；应用由 API 启动时
  `MigrateAsync` 完成。项目 RootNamespace 是 `TreeGraph.Api.NodeEavSky`，
  迁移命名空间 `TreeGraph.Api.NodeEavSky.Data.Migrations` 为正常。
- 文档代码因仓库真实 API 差异编译失败时，以仓库真实定义为准做最小修正，
  并 Grep 抽查 1~3 个标志性符号确认关键修复点真实存在。

## 7. 报告

列出：文件、方式（逐字覆盖/补丁/适配修正）、字节数（脚本已输出）、
构建与测试结果、是否生成迁移；适配修正逐条给出"文档写法 → 实际写法 → 原因"。
不主动 git 提交。
