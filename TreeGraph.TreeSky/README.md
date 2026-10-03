# TreeGraph.TreeSky

Blazor 树组件库，从 `APromisedLand.Razor` 的 `TreeSky` 组件精简移植而来，作为独立 Razor 类库承载。用于在任意 Blazor 宿主中以泛型方式渲染一棵可懒加载、可增删改、可排序/移动的分级树。

## 移植背景与决策

原方案曾尝试将 `APromisedLand.Razor/DiberyTree` 下的 197 个文件物理复制到 `TreeGraph.Blazor` 中并全局替换命名空间，结果文件平铺、依赖污染严重，无法编译通过。回撤后重新采用**独立项目 + 精简闭包**策略：

- **新建独立 Razor 类库项目** `TreeGraph.TreeSky`，与原宿主代码物理隔离。
- **只复制组件本身及其真实闭包**，不复制外围 UI（MessageDialog、ButtonLoadingSky 等），对必须的外部依赖创建精简承载（薄包装）。
- **被引用组件在外围的代码**（如附件页面、UnitTree/CategoryTree 的具体实现、Attribute 属性面板等）**完全不复制**。
- **宿主通过 ProjectReference + DI 扩展接入**，零侵入源项目代码。

## 依赖包

| 包 | 版本 | 说明 |
|---|---|---|
| Microsoft.NET.Sdk.Razor | — | SDK |
| Microsoft.AspNetCore.Components.Web | 10.0.12 | |
| Microsoft.Extensions.Http | 10.0.0 | |
| MudBlazor | 9.11.0 | 核心 UI 组件 |
| MudBlazor.Extensions | 9.11.0 | `DialogOptionsEx`、`ShowExAsync`、`MudDialogDragMode` |

> 不需要 `CodeBeam.MudBlazor.Extensions`（旧 `MudExtensions` 命名空间的包）。

## 目录结构

```
TreeGraph.TreeSky/
├── _Imports.razor                     # 公共命名空间
├── Models/
│   ├── ITreeNodeBase.cs               # 树节点核心接口
│   ├── IHierarchyTreeNodeBase.cs
│   ├── IArchivableTreeNodeBase.cs
│   ├── StringTreeNode.cs              # ★ 内建字符串节点（Name 即显示文本，开箱即用）
│   ├── TreeNodeDto.cs                 # 泛型节点 DTO
│   ├── TreeQueryParams.cs
│   └── ApiResponse.cs                 # 统一 API 响应壳
├── Services/
│   ├── ITreeClientService.cs          # 宿主需实现的客户端服务接口
│   └── DiberyTreeApiClient.cs         # 泛型 HTTP API 客户端（增删改/排序/移动）
├── Navigation/
│   ├── ITreeNavigationHistoryService.cs
│   ├── TreeNavigationHistoryService.cs
│   └── HistoryEntry.cs
├── Components/
│   ├── Base/
│   │   ├── TreeSky.razor (+ 4 partial) # 核心树组件
│   │   ├── TreeDialogPageSky.razor
│   │   └── TreeSelectDialogSky.razor  # 节点/父节点选择对话框
│   ├── Nodes/
│   │   ├── DialogTreeSky.razor         # 树节点操作外壳
│   │   ├── TreeNodeActionsDialog.razor # 操作菜单（移动/编辑/删除/排序）
│   │   ├── TreeNodeViewDialog.razor
│   │   ├── TreeNodeEditDialog.razor    # 编辑表单
│   │   ├── TreeNodeParentSelectDialog.razor
│   │   └── TreeNodeSortDialog.razor    # 拖拽排序
│   └── Shared/
│       ├── DialogSky.razor             # MudDialog 薄包装
│       ├── DialogPageSky.razor         # 工具栏/取消/提交按钮
│       ├── ProgressCircularSky.razor
│       └── BoolFieldSky.razor
├── Extensions/
│   └── TreeSkyServiceCollectionExtensions.cs   # AddTreeSky()
└── 根目录工具类
    ├── TreeHelper.cs                   # TreeItemData 扩展 + 常量
    ├── BlazorService.cs                # DialogOptionsEx 单例
    ├── MessageService.cs               # DeleteBox/BoolBoxAsync/Details/Success/Warning/Info/Error
    ├── TreeNodeDialogService.cs        # 对话框编排（ShowDialogPage/ShowTreeSelect/ShowCreate/ShowEdit/ShowSort/ShowMove）
    └── 数据模型（NodeAction、NodeTemplate、NodeActionResult、ParentSelectResult、SortResult、DialogConfig）
```

## 使用方法

### 1. 宿主 csproj 添加引用

```xml
<ProjectReference Include="..\TreeGraph.TreeSky\TreeGraph.TreeSky.csproj" />
```

### 2. Program.cs 注册

```csharp
// MudBlazor 基础服务（宿主原有）
builder.Services.AddMudServices();

// TreeSky 组件库（含 BlazorService/MessageService/TreeNodeDialogService<>/导航历史/DiberyTreeApiClient<> + MudExtensions）
builder.Services.AddTreeSky();

// ★ 由宿主自行注册：具体节点类型的客户端服务
builder.Services.AddScoped<ITreeClientService<YourNode>, YourNodeClientService>();
```

`AddTreeSky` 内部会注册一个命名 `HttpClient("TreeSky")`；如果后端有真实地址：

```csharp
builder.Services.AddTreeSky(configureClient: c => c.BaseAddress = new Uri("https://your-api/"));
```

### 3. 页面中使用

```razor
@page "/your-tree-page"
@rendermode InteractiveServer

<TreeSky TItem="YourNode">
    <EditTemplate>
        <MudTextField @bind-Value="context.Name" Label="名称" Required="true" />
    </EditTemplate>
</TreeSky>
```

### 4. 静态资源（若需 MudBlazor.Extensions 的 CSS/JS）

在 `App.razor`（或 `_Host.cshtml`）中追加：

```html
<link href="_content/MudBlazor.Extensions/mudBlazorExtensions.min.css" rel="stylesheet"/>
<script type="module" src="_content/MudBlazor.Extensions/MudBlazor.Extensions.lib.module.min.js"></script>
```

## 裁剪说明

| 模块 | 决策 | 理由 |
|---|---|---|
| **Attributes 属性子系统**（30+ 文件） | ❌ 未复制 | 仅被属性面板引用，与树核心无关。保留「属性」动作入口，点击后提示模块未包含。 |
| **文件/图片/视频/位置附件 4 页面** | ❌ 未复制 | 仅被属性面板引用。DialogTreeSky 工具栏移除 4 个按钮；TreeNodeDialogService 移除 4 个附件方法。 |
| **UnitTree / CategoryTree 具体树** | ❌ 未复制 | 具体业务类型，属于宿主层。TreeSelectDialogSky 移除 `EditFunc` 和编辑按钮耦合。 |
| **MessageDialog**（旧消息弹窗） | ❌ 未复制 | 依赖复杂。MessageService 改用 MudBlazor 原生 `MessageBox` 和 `Snackbar`。 |
| **ButtonLoadingSky**（自定义加载按钮） | ❌ 未复制 | 通用组件无移植必要。DialogPageSky 改用普通 `MudButton`。 |
| **TreeNodeDeleteDialog** | ❌ 未复制 | 源码中该文件已被注释，死代码。 |
| **TreeNodeFormModel / TreeNodeDialogResult** | ❌ 未复制 | 仅被已移除的附件/属性模块引用。 |
| **SolutionService / UnitsOfMeasure** | ❌ 未复制 | 仅被附件页面引用。 |
| **TreeDragDropHelper** | ❌ 未复制 | 仅被附件页面引用。 |

## 已知修复记录

### 1. DI 注册：`TryAddTransient<HttpClient>` 导致 ApiClient 拿到空配置

**问题**：`AddHttpClient("TreeSky")` 内部通过 `TryAddTransient<HttpClient>()` 注册了一个无名 HttpClient。库内若同样用 `TryAddTransient` 注册 `HttpClient`，会因服务已存在而静默失效，导致 `DiberyTreeApiClient<>` 注入的 `HttpClient` 没有 `BaseAddress`，也无任何消息处理器。

**修复**（`Extensions/TreeSkyServiceCollectionExtensions.cs`）：将 `TryAddTransient` 改为 `AddTransient`：

```csharp
services.AddTransient(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName));
```

### 2. 移动对话框：选择节点后无法关闭

**问题**：`TreeSelectDialogSky` 原逻辑 `_selectLeaf = OriginalNode == null && ClientService.SelectLeaf`。在移动场景中 `OriginalNode` 非空，导致 `_selectLeaf = false`，于是点击叶子节点时不会关闭对话框。

**修复**：将 `_selectLeaf` 改为始终取自 `ClientService.SelectLeaf`（表示**仅选叶子**这一约束）。移动场景增加额外校验：
- 禁止选择节点自身；
- 禁止选择自身后代（沿候选节点 Parent 链向上检查，防环）；
- 对话框工具栏增加「移到根节点」按钮。

### 3. 内存存储移动判定：按 `ParentId` 字段比对漏判

**问题**：客户端在发 Update 请求前已把 `node.ParentId` 改为了新父 ID，存储层若仍按 `newParentId != node.ParentId` 比对，永远为 `false`，导致节点不会被实际挂接到新父下。

**修复**：存储层 `Update` 改为按图挂接关系（`node.Parent?.Id`）判断是否需要重新挂接。

### 内建 string 节点类型

库自带 `StringTreeNode`（`TreeGraph.TreeSky.Models`）：以 `Name` 字符串为显示文本，满足泛型约束 `class, ITreeNodeBase<T>, new()`。`System.String` 本身不可用作 `TItem`（sealed、无无参构造），需要字符串节点时直接用 `StringTreeNode` 即可，也可继承扩展字段。

```razor
<TreeSky TItem="StringTreeNode">
    <EditTemplate>
        <MudTextField @bind-Value="context.Name" Label="名称" Required="true" />
    </EditTemplate>
</TreeSky>
```

## 演示示例

宿主 `TreeGraph.Blazor` 内基于 `StringTreeNode` 实现了一套可运行的**内存演示后端**，无需真实 API：

- `InMemoryTreeStore`：进程内单例内存存储，构造时一次性播种演示数据。
- `DemoTreeClientService`：`ITreeClientService<StringTreeNode>` 实现。
- `DemoTreeApiHandler`：终端 `DelegatingHandler`，拦截 `StringTreeNode/*` HTTP 请求并转发到同一内存存储。
- 演示页：`/tree-sky-demo`（菜单入口「TreeSky 演示」）。

浏览器实测验证通过：浏览/创建/编辑/排序/移动/删除全部可用。新增 7 个管道集成测试（ApiClient → Handler → Store），全量 88 个测试零回归。

## 约束与待办

- **泛型约束**：`TItem : class, ITreeNodeBase<TItem>, new()`。`System.String` 不满足（sealed、无 `new()`），库已内建 `StringTreeNode` 作为字符串承载类型。
- **宿主需自行实现**：`ITreeClientService<TNode>`（数据读取/排序/标题配置）。
- **Move 后端路由**：当前组件通过 `UpdateNodeAsync` 完成移动（旧 `MoveNodeAsync` 备用）。若后端 controller 实现了 `move` 路由，可直接调用 `ApiClient.MoveNodeAsync`。
