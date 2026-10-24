# R1 · DiberyTree 解绑 · 施工任务

> 依据 `specs/node-sky/r1-unbind-diberytree/spec.md`。顺序执行，每步可编译性由最终全量构建兜底。

- [x] T1 新建 `TreeGraph.Blazor.Shared/TreeEavSky/TreeNodeItem.cs`（spec §4.1 完整定义）
- [x] T2 改 `TreeEavSky/TreeHelper.cs`（5 个扩展方法；`ToTreeItemData`→`ToTreeNodeItem`）
- [x] T3 改 TreeEavSky 组件 4 文件：`TreeEavSky.razor.cs` / `.Loading.cs` / `.Node.cs` / `.Action.cs`
- [x] T4 改边界 razor 3 文件：`TreeEavSky.razor`（2 处强转 + 2 参数类型）、`TreeDialogPageSky.razor`、`TreeSelectDialogSky.razor`
- [x] T5 改契约/对话框/排序 4 文件：`Contracts/NodeTemplate.cs`、`Dialogs/TreeNodeDialogService.cs`、`Dialogs/BlazorService.cs`、`Nodes/TreeNodeSortDialog.razor`
- [x] T6 改四树页面：`Trees/Unit|Space|Category|Knowledge/*DialogPage.razor`（各 2 处）
- [x] T7 改测试 4 文件（先 Read 后 Edit）：`TreeHelperTests.cs`、`TreeHelperIterationTests.cs`、`Components/TreeEavSkyComponentTests.cs`、`Services/TreeNodeDialogServiceTests.cs`
- [x] T8 验证：无锁进程 → `dotnet build APromisedLand.sln` 0 错误 → `dotnet test TreeGraph.Blazor.Shared.Tests` 235+1 全绿 → grep 残留核对（spec §5.4）
- [x] T9 向用户汇报 R1 完成，待命进入 R2（不 commit/push）
