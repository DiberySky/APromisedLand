# EAV 文件属性接入 FileStorageApi 上传下载 — 实施计划

## Context（为什么做）

`TreeGraph.Blazor.Shared` 的 EAV `file` 属性渲染器 [FileField.razor](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/NodeEavSky/Components/FieldRenderers/FileField.razor) 目前是"基础版"——仅一个 URL 文本框，存储 JSON 形如 `{ url, fileName, mimeType, sizeBytes }`，**完全没有调用** `IFileStorageClient`。

而后端 (TreeGraph.Api) 与 seeder ([StringTreeDemoSeeder.cs](file:///d:/APromisedLand/TreeGraph.Api/Data/Seeding/StringTreeDemoSeeder.cs) `BuildFileMeta` L768-L780) 写入的是与 `FileMetadataDto` 对齐的 7 字段子集：`docId / fileName / contentType / size / sha256 / version / status`。

结果：前端 `FileField` 渲染 seeder 写入的数据时字段全部错位（`url` 读不到、`mimeType` 为空、`sizeBytes` 为 0），且用户在 UI 上"编辑"文件属性产生的 JSON 与后端契约不一致。

本计划：升级 `FileField.razor` 真正接入 `IFileStorageClient`（分块上传/下载/删除），统一 JSON shape 为 7 字段子集；在 `StringTreeDemoSeederTests` 补强 GET 全字段断言 + 新增 PUT round-trip 测试；在 `TreeGraph.Blazor.Shared.Tests` 新增 `FileField` 组件单测。

## 设计决策

| 编号 | 决策 | 选择 | 理由 |
|---|---|---|---|
| D1 | EAV 存储的 JSON shape | **7 字段子集** `docId/fileName/contentType/size/sha256/version/status` | 与 seeder `BuildFileMeta` 完全对齐；现有测试 #21 已断言这 7 字段。存全量 `FileMetadataDto`（含 Id/Tenant/ObjectKey/CreatedAt/UpdatedAt）会破坏 seeder 已落库行的形状并使 #21 失败。`Id`（Guid）在下载/删除时按需通过 `GetFileByDocIdAsync` 现取。 |
| D2 | Complete 后取回完整元数据 | **Complete → `GetFileByDocIdAsync(docId, version)` → 投影到 7 字段** | `CompleteUploadResponse` 缺 `FileName/ContentType/Status`。失败回退：`CompleteUploadResponse` 字段 + 原请求 `FileName/ContentType` + `status="active"`。 |
| D3 | Fingerprint | `$"{fileName}:{size}"` | 客户端零成本，支持误刷新续传；不计算全文 SHA256（避免读两次）。 |
| D4 | 心跳 | `PeriodicTimer(300s)` + `CancellationTokenSource` | 上传开始启动，完成/异常/取消时 dispose。 |
| D5 | 删除 | 二次确认 → `GetFileByDocIdAsync` → `DeleteFileAsync` → `ValueChanged(null)` | 破坏性操作须确认。 |
| D6 | 下载 | 两步：`GetFileByDocIdAsync` 拿 `Id` → `DownloadFileAsync(id)` → JS 触发浏览器下载 | seeder JSON 不含 `Id`，现取现用。 |
| D7 | ChunkSize 约束 | 文件 ≥ 256KB：`Math.Min(fileSize, 8MB)`；< 256KB：**不传** `ChunkSize`（服务端默认 8MB → 1 块） | `InitiateUploadRequest.ChunkSize` 有 `[Range(256KB, 16MB)]`，传 < 256KB 会 400。 |
| D8 | PUT round-trip 测试目标实体 | 新实体 ID `round-trip-node`（非 demo-node-001） | PUT 是全量替换（`EavController.Put` L88 → `SaveAsync`），写到新 ID 只带 `attachment` 一键，不污染 4 个 demo 节点的其它属性断言。 |

## 实施步骤

### 1. 升级 FileField.razor

**文件**：[FileField.razor](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/NodeEavSky/Components/FieldRenderers/FileField.razor)

保留与 `DynamicForm.razor` 的契约（[L84-L87](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/NodeEavSky/Components/DynamicForm.razor)）：`[Parameter] AttributeSchemaDto Attr`、`[Parameter] JsonElement? Value`、`[Parameter] EventCallback<JsonElement?> ValueChanged`。

**新增 `@inject`**：`IFileStorageClient FileStorage`（来自 `TreeGraph.Blazor.Shared.FileStorageSky.Services`）、`IDialogService Dialog`、`IJSRuntime JS`；保留 `IPlatformContext Platform`。

**Markup 大纲**：
- 上传入口：`<InputFile OnChange="OnFileSelected" />`（包在 `MudButton` 内），`accept="*/*"`
- 上传中：`<MudProgressLinear Value="_progress" Max="100" />` + 文本 `已上传 @_uploadedChunks / @_totalChunks 块`
- 已有文件信息卡（`_meta != null`）：文件名 / 类型 / 大小（`FormatSize`）/ SHA256 前 16 位 + 下载按钮 + 删除按钮
- 错误提示：`_errorMessage`

**`@code` 方法**：
- `OnParametersSet`：从 `Value` 解析 7 字段填充 `_meta`（docId 缺失则 `_meta=null`）
- `async Task OnFileSelected(InputFileChangeEventArgs e)`：取 `IBrowserFile`，计算 fingerprint，`InitiateUploadAsync`；若 `Resumed` 则 `GetUploadStatusAsync` 拿已收分块跳过；启心跳；进入 `UploadAsync`
- `async Task UploadAsync(IBrowserFile, InitiateUploadResponse)`：`file.OpenReadStream(maxAllowedSize: 2L*1024*1024*1024)` → 按 `init.ChunkSize` 循环 `ReadAsync` → `UploadChunkAsync` → 更新 `_progress/_uploadedChunks` + `StateHasChanged`
- `async Task HeartbeatLoop(Guid, CancellationToken)`：`PeriodicTimer(300s)` 循环调 `HeartbeatAsync`；异常吞掉
- `async Task BuildValueJsonAndCommit(Guid uploadId, IBrowserFile file)`：`CompleteUploadAsync` → `GetFileByDocIdAsync` 取权威元数据 → 投影 7 字段 → `JsonSerializer.SerializeToElement(meta, WebJsonOpt)` → `ValueChanged.InvokeAsync(json)`；失败走回退分支
- `async Task OnDownload()`：`GetFileByDocIdAsync(_meta.DocId, _meta.Version)` → `DownloadFileAsync(full.Id)` → `JS.InvokeVoidAsync("treegraphFile.downloadFromStream", fileName, DotNetStreamReference.Create(stream))`
- `async Task OnDeleteAsync()`：`Dialog.ShowMessageBox` 确认 → `GetFileByDocIdAsync` → `DeleteFileAsync` → `_meta=null` + `ValueChanged.InvokeAsync(null)`

**`FileMeta` 内部类**改为 7 字段：`DocId/FileName/ContentType/Size/Sha256/Version/Status`。

### 2. 新增 JS 下载互操作文件

**文件**：`d:\APromisedLand\TreeGraph.Blazor.Shared\wwwroot\js\fileDownload.js`（RCL 静态资产，宿主通过 `_content/TreeGraph.Blazor.Shared/js/fileDownload.js` 引用）

导出一个函数 `treegraphFile.downloadFromStream(fileName, streamRef)`：用 `streamRef.arrayBuffer()` → `Blob` → `URL.createObjectURL` → 创建 `<a download>` 点击 → 撤销 URL。**这是本计划唯一新增的非 .razor 文件**。宿主 App 需在 `index.html`/`_Host.cshtml` 加 `<script src="_content/TreeGraph.Blazor.Shared/js/fileDownload.js"></script>`（实施时在 PR 描述中注明）。

### 3. 加固 StringTreeDemoSeederTests

**文件**：[StringTreeDemoSeederTests.cs](file:///d:/APromisedLand/TreeGraph.Api.Tests/Tests/StringTreeDemoSeederTests.cs)

**3.1 加固 #12 `Api_GetEntity_ReturnsAllProperties`**（[L345-L375](file:///d:/APromisedLand/TreeGraph.Api.Tests/Tests/StringTreeDemoSeederTests.cs)）：在 L374 现有 `docId` 断言后追加 6 个断言（`fileName/contentType/size/sha256/version/status` 全字段），镜像 #21 L821-L828。不改方法名。

**3.2 新增 #23 `Api_PutEntity_RoundTripsFileAttachmentMetadata`**（插入到 #22 之后）：
1. `Factory.CreateClient()` 构造 HttpClient
2. 构造 7 字段 payload（`docId="put-test-doc"`、`fileName="round-trip.pdf"`、`contentType="application/pdf"`、`size=1024L`、`sha256="abc123"`、`version=2`、`status="active"`），用 `JsonSerializer.SerializeToElement(..., JsonOpt)` 转 `JsonElement`，包成 `Dictionary<string, object?> { ["attachment"] = jsonEl }`
3. `PUT /api/eav/{SpaceEntityType}/entities/round-trip-node`（`PutAsJsonAsync`）→ 断言 `NoContent`（204）
4. `GET /api/eav/{SpaceEntityType}/entities/round-trip-node` → 断言 `attachment` 7 字段全部 round-trip 保持
5. （可选清理）`PATCH` 同一 endpoint 传 `{ ["attachment"] = null }` 删除该属性 — 非必须，因 `round-trip-node` 不影响其它测试

### 4. 新增 FileField 组件单测

**新文件**：`d:\APromisedLand\TreeGraph.Blazor.Shared.Tests\NodeEavSky\FileFieldTests.cs`

**Setup**（镜像 [DynamicFormResponsiveTests.cs](file:///d:/APromisedLand/TreeGraph.Blazor.Shared.Tests/NodeEavSky/DynamicFormResponsiveTests.cs) L12-L18）：
```
public class FileFieldTests : BunitTestBase {
  private readonly Mock<IFileStorageClient> _storage;
  public FileFieldTests() {
    JSInterop.Mode = JSRuntimeMode.Loose;
    Services.AddMudServices();
    Services.AddSingleton<IPlatformContext>(new FakePlatformContext());
    _storage = new Mock<IFileStorageClient>();
    Services.AddSingleton(_storage.Object);
  }
}
```

**测试方法**：
| # | 方法名 | Mock Setup | 断言 |
|---|---|---|---|
| 1 | `Renders_SeededFileMetadataDto_ShowsAllFields` | 不调方法 | 传 7 字段 `Value` → markup 含文件名/大小/contentType/sha256 |
| 2 | `Upload_Flow_CallsInitiateChunkComplete_AndWritesBack7FieldJson` | Initiate→`{TotalChunks=1,ChunkSize=8MB}`；UploadChunk→`{ReceivedCount=1,TotalChunks=1}`；Complete→`{DocId="up-1",Version=1,Size=1024,Sha256="sha-1"}`；GetFileByDocId→完整 `FileMetadataDto` | 触发 `InputFile` 选文件（用内部 `FakeBrowserFile : IBrowserFile` 实现）→ Verify Initiate/UploadChunk/Complete/GetFileByDocId 各 Once + `ValueChanged` 写回 7 字段 JSON |
| 3 | `Download_CallsGetFileByDocIdThenDownloadFileAsync` | 渲染时 `Value` 已是 7 字段；GetFileByDocId→`{Id=Guid}`；DownloadFile→`MemoryStream` | 点下载按钮 → Verify 两调用各 Once |
| 4 | `Delete_Confirms_AndClearsValue` | GetFileByDocId→meta；DeleteFile→true；`IDialogService` 用 `Services.AddMudServices()` 后覆盖注册 `Mock<IDialogService>` 并 `Setup(s => s.ShowMessageBox(...)).ReturnsAsync(true)` | 点删除 → Verify DeleteFile Once + `ValueChanged` 收到 null |

`FakeBrowserFile`：测试文件内私有类，实现 `IBrowserFile`（Name/Size/ContentType/LastModified/OpenReadStream 返回固定大小 `MemoryStream`）。

### 5. DI 注册说明（不改代码，仅文档）

消费方 App（TreeGraph.Blazor / DiberyBlazorWebSky / APromisedLand.Maui 等）的 `Program.cs` 必须已注册 `IFileStorageClient`：
```
builder.Services.AddHttpClient<IFileStorageClient, FileStorageApiClient>(c => c.BaseAddress = new Uri(fileStorageApiBaseUrl));
builder.Services.Configure<FileStorageSkyOptions>(o => { ... });
```
本计划不改任何宿主 `Program.cs`。bUnit 测试中直接 `Services.AddSingleton(_storage.Object)` 注入 mock。

## 验证

**编译**（按依赖顺序）：
```
dotnet build d:\APromisedLand\TreeGraph.Blazor.Shared\TreeGraph.Blazor.Shared.csproj
dotnet build d:\APromisedLand\TreeGraph.Blazor.Shared.Tests\TreeGraph.Blazor.Shared.Tests.csproj
dotnet build d:\APromisedLand\TreeGraph.Api.Tests\TreeGraph.Api.Tests.csproj
```

**运行测试**：
```
dotnet test d:\APromisedLand\TreeGraph.Blazor.Shared.Tests\TreeGraph.Blazor.Shared.Tests.csproj --filter "FullyQualifiedName~FileFieldTests"
dotnet test d:\APromisedLand\TreeGraph.Api.Tests\TreeGraph.Api.Tests.csproj --filter "FullyQualifiedName~StringTreeDemoSeederTests"
```
重点：`FileFieldTests` 全 4 条；`StringTreeDemoSeederTests.Api_GetEntity_ReturnsAllProperties`（加固后）+ `Api_PutEntity_RoundTripsFileAttachmentMetadata`（新增）；回归 `FileAttribute_Metadata_Written_ForDemoNode001`（#21）+ `FileAttribute_Metadata_Differs_AcrossNodes`（#22）保持绿；回归 `DynamicFormResponsiveTests` 全集确认 DynamicForm→FileField 装配未破。

## 风险与注意事项

1. **存 7 字段子集 vs 全量 FileMetadataDto**：选 7 字段以对齐 seeder。副作用：每次下载/删除需多一跳 `GetFileByDocIdAsync`。后续若倾向"存 Id 直下载"，需同时改 seeder `BuildFileMeta` 与 #21/#22，影响面更大——本计划选最小破坏路径。
2. **破坏性删除**：清空属性值即删二进制。已加二次确认。顺序为"先删二进制再清 EAV"，若 `ValueChanged` 失败会留"二进制没了、EAV 还显示"的不一致，靠 `_errorMessage` 提示用户重试清空。
3. **ChunkSize Range**：< 256KB 文件不传 `ChunkSize`（D7）。实施前精读 [FileStorageApiClient.InitiateUploadAsync](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/FileStorageSky/Services/FileStorageApiClient.cs) L34-L45 确认 `null` 不被序列化成 0（`PostAsJsonAsync` 默认忽略 null 成员，应安全）。
4. **IBrowserFile.OpenReadStream 默认 500KB 上限**：必须显式传 `maxAllowedSize: 2L*1024*1024*1024`。
5. **JS 下载互操作**：新增 `wwwroot/js/fileDownload.js`（RCL 静态资产）；宿主需加 `<script>` 引用——在 PR 描述中注明，本计划不改宿主 `index.html`。
6. **#23 PUT 测试**：依赖 `SaveAsync` 写入新 `EntityId`（自动创建 AttributeValue 行，EAV 无独立 EavEntity 表）。若 204 但 GET 返回 404，回退为 PATCH `demo-node-001`（仅 attachment）+ finally 恢复原始值。
