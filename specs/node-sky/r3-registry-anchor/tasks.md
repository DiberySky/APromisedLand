# R3 · 锚点迁移到 nodes 注册表 · 施工任务

> 依据 `specs/node-sky/r3-registry-anchor/spec.md`。范围：§8.5 + 边注册进 nodes（不含 §8.6 边表重建）。

## 阶段一：共享层去 Inode

- [x] **T1** `UnitTree.cs` / `SpaceTree.cs`：类声明去 `, Inode`
- [x] **T2** `CategoryTree.cs` / `KnowledgeTree.cs`：`IEquatable<X>, Inode` → `IEquatable<X>`
- [x] **T3** `LineNode.cs`：类声明去 `: Inode`
- [x] **T4** `EdgeDefinition.cs`（`EdgeInstance` 类）：去 `: Inode`、删显式 `Inode.Id/Name`、加 `[JsonIgnore] Node? Node` 导航
- [x] **T5** 构建 `TreeGraph.Shared`，0 错误

## 阶段二：守卫收敛

- [x] **T6** `InodeOwnerGuard.cs`：新增 `INodesRegistryGuard`（`NodeExistsOfTypeAsync` / `HostTypeExistsAsync`）
- [x] **T7** `InodeOwnerGuard.cs`：`Registry` 新增 `EnsureNodeOfTypeAsync`（精确守卫 → nodes 类型感知 → 未认领）
- [x] **T8** `NodeOwnerGuard.cs` → 重写为 `NodesRegistryGuard`（`NodeType=""`、类型感知、单表名称解析 + UnitTree 简称 + 边投影）
- [x] **T9** 删除 `TreeEntityOwnerGuard.cs`（4 守卫）、`LineNodeOwnerGuard.cs`、`EdgeOwnerGuard.cs`
- [x] **T10** `EdgeNodeValidator.cs`：改为调 `EnsureNodeOfTypeAsync`
- [x] **T11** `Program.cs`：守卫注册 8 → 2

## 阶段三：EdgeInstance 注册进 nodes

- [x] **T12** `TreeGraphDbContext.ConfigureEdges`：`EdgeInstance` 加 `HasOne(Node).WithOne().HasForeignKey<EdgeInstance>(x => x.EdgeId).OnDelete(Restrict)`
- [x] **T13** `EdgeInstanceService.CreateCoreAsync`：`EdgeId = Guid.NewGuid().ToString()`；同事务 `Nodes.Add` + `EdgeInstances.Add`
- [x] **T14** `EdgeInstanceService.DeleteCoreAsync`：前置「被其他边引用」检查；`Remove(edge)` + `Remove(nodeRow)` 同一次 `SaveChanges`
- [x] **T15** `dotnet ef migrations add RegisterEdgesInNodes`；手工在 `AddForeignKey` 前插入回填 SQL；`Down` 删 FK + 注销 nodes 行
- [x] **T16** 快照核对：`edge_instances` FK 存在、`nodes` 无新增列

## 阶段四：测试同步

- [x] **T17** `EdgeGuardApiFactory.cs`：改注册 `NodesRegistryGuard`
- [x] **T18** 全仓 grep `Inode` / `UnitTreeInodeGuard` / `NodeOwnerGuard` 清理残留引用

## 阶段五：验证

- [x] **T19** `dotnet build APromisedLand.sln` 0 错误（2 条预存 EF 版本警告）
- [x] **T20** `dotnet test TreeGraph.Blazor.Shared.Tests` → 235 通过 + 1 跳过
- [x] **T21** `dotnet test TreeGraph.Api.Tests` → 322
- [x] **T22** `dotnet test TreeGraph.MafWfApi.Tests` → 65
- [x] **T23** `dotnet test TreeGraph.FileStorageApi.Tests` / `TreeGraph.Blazor.Tests` → 153 / 71

## 阶段六：评审

- [x] **T24** 独立评审：核对 AC1–AC11
