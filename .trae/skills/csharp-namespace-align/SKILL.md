---
name: csharp-namespace-align
description: 在 .NET 解决方案中重命名 C# 命名空间并同步对齐目录、using 与 Razor @using，含相对限定名补漏、保持 BOM 的批量替换（排除 bin/obj）及逐项目 dotnet build 验证。当用户要求“对齐命名空间”“重命名命名空间”或移动目录/项目后要求 namespace 跟随目录时使用。不用于不涉及命名空间变更的纯文件移动。
---

# C# 命名空间重命名与对齐

在 SDK 风格 .NET 项目（net10.0，含 Blazor/Razor）中，把命名空间 `A.B.C` 整体改为 `A.B.D`，并可选同步重命名物理目录。核心原则：**先冻结范围清单，再机械替换，最后靠编译器收敛相对限定名漏网之鱼**。

## 0. 确认参数

向用户确认（或从指令中提取）三项：

- 旧命名空间前缀（如 `TreeGraph.Shared.Eav`）
- 新命名空间前缀（如 `TreeGraph.Shared.NodeEav`）
- 是否同步重命名目录（本仓库约定命名空间跟随目录，如 `Eav` → `NodeEav`）；目录名与命名空间末段不一致时必须向用户澄清

## 1. 建立现状清单（先扫描，不改动）

1. 用 Grep（output_mode=count）在工作区扫描完整前缀 `TreeGraph\.Shared\.Eav`，glob 用 `*.{cs,razor}`，记录文件数与总命中数。
2. 紧邻标识符碰撞检查：搜索 `<旧前缀>[A-Za-z0-9_]`。
   - 无命中 → 可安全做纯字符串替换。
   - 有命中（如旧名是另一命名空间的前缀）→ 不能整体替换，需逐文件人工判定后再执行。
3. 额外覆盖 Razor：确认 `.razor` 中的 `@using`、`_Imports.razor` 已被同一前缀扫描覆盖（本仓库 @using 与 C# using 文本同形）。

## 2. 重命名物理目录（如需要）

- 用 Shell `Move-Item <旧目录> <新目录>` 在**批量替换之前**完成。
- SDK 风格 csproj 默认隐式包含 `**/*.cs`，目录改名**不需要**改 csproj；禁止顺手新增显式 `<Compile Include>`（会触发 NETSDK1022）。
- 仅当构建报“找不到文件”时才检查 csproj 是否存在硬编码路径。

## 3. 批量替换（使用随附脚本）

运行 `scripts/Rename-Namespace.ps1`（PowerShell 5.1+ 兼容）：

```powershell
powershell -ExecutionPolicy Bypass -File .trae\skills\csharp-namespace-align\scripts\Rename-Namespace.ps1 `
  -Root "d:\APromisedLand" `
  -OldNamespace "TreeGraph.Shared.Eav" `
  -NewNamespace "TreeGraph.Shared.NodeEav"
```

脚本特性（勿用裸 `Set-Content`/`(Get-Content)|Set-Content` 替代）：

- 通过 `[System.IO.File]::ReadAllText/WriteAllText` 读写，**按原文件是否带 UTF-8 BOM 分别选编码写回**，中文注释不乱码。
- 硬排除路径段 `\bin\`、`\obj\`，避免污染生成代码。
- 默认处理 `*.cs` 与 `*.razor`。
- 输出实际修改文件数；该数必须与第 1 步清单一致，不一致先排查再继续。

## 4. 残留与相对限定名补漏（必做，最易漏）

全字符串替换抓不到利用命名空间层级的**相对限定引用**。按序检查：

1. Grep 完整旧前缀，期望 0 命中。
2. Grep 相对写法 `<中段>.<旧末段>`（例如旧名 `TreeGraph.Shared.Eav` 时搜 `Shared\.Eav`）。
3. Grep 非点开头的子段限定，例如 `[^.\w]Eav\.Dtos`。
4. 对每处漏网（形如 `new Shared.Eav.Dtos.XxxRequest`）用 Edit 单独改为新相对路径。
5. 再跑一次第 1、4.2、4.3 项扫描，全部 0 命中才进入构建。

## 5. 逐项目构建验证

对**所有**引用该命名空间的项目分别 `dotnet build <proj> -v q --nologo`，至少覆盖：

- 服务端 `TreeGraph.Api`
- Blazor 宿主 `TreeGraph.Blazor`
- 全部相关测试项目（`TreeGraph.Api.Tests`、`TreeGraph.Blazor.Shared.Tests`、`TreeGraph.Blazor.Tests`、`TreeGraph.Blazor.E2E.Tests`，按命中清单取舍）
- MAUI 项目仅在其文件有命中时处理（通常需 workload，引用面未变可说明跳过）

出现错误时用 `Select-String -Pattern 'error ' -Context 0,2` 取详情，修复后重跑该项目；其余项目可并行构建。验收标准是每个项目“0 个错误”（既有的 analyzer 警告如 CS8669/MUD0002 与本操作无关，不处理）。

## 6. 汇报

用简表给出：目录变更、新旧命名空间、修改文件数与命中数、相对限定名补漏处数、各项目构建结果。不创建提交，除非用户明确要求。

## 已验证的本仓库实例

- `TreeGraph.StringTree.Contracts` → `TreeGraph.Shared.StringTreeSky.Contracts`（随项目迁移到 TreeGraph.Shared/StringTreeSky/Contracts）。
- `TreeGraph.Shared.Eav(.Dtos)` → `TreeGraph.Shared.NodeEav(.Dtos)`，目录 `Eav` → `NodeEav`，90 个文件 104 处 + 1 处 `Shared.Eav.Dtos.EavQueryRequest` 相对限定补漏。
