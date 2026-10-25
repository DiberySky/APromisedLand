# 验收清单 — 搜索页结果行直开宿主节点操作对话框

## 服务端
- [x] `GET {id}` 端点在 TreeControllerBase 生效（200/404/500 镜像现有风格），与 roots/full/双段路由无冲突
- [x] 4 个 TreeService 实现 GetNodeByIdAsync：Include Node + HasChildren 用 AnyAsync 重算 + 填 Parent/Value.Parent
- [x] TreeApiClient.GetNodeByIdAsync：404→null

## 编排器
- [x] TreeNodeActionOrchestrator 为静态逻辑外的唯一操作编排入口；树侧与搜索侧共用
- [x] View/Edit/Attribute/Sort 执行后重开宿主对话框；AddChild/Delete/Move 执行后退出（行为与重构前一致）
- [x] TreeEavSky.razor.Action.cs 薄壳化：Handle* 与 4 个死代码方法已删除，公开面 ShowNodeActionsAsync/ExecuteNodeActionAsync 保留

## 搜索页
- [x] ⋯ 维护图标按 MatchKind/HostType 分发：树宿主→TreeNodeActionsDialog 连续操作；LineNode→LineNodeActions.OperateAsync；裸节点/实体→NodeAttributesDialog
- [x] 树定位 / AI 综合图标保留为行内独立图标
- [x] 删除后 OnDeleted 移除结果行；变更后 OnChanged 重跑搜索

## 模板共享
- [x] 4 棵树的编辑字段区抽为 XxxTreeEditFields 组件，TreeEditTemplates 静态模板薄包装；4 个 DialogPage 与搜索侧共用同一 markup

## 验证
- [x] `dotnet build` 全解决方案 0 错误
- [x] `dotnet test`：Blazor.Shared 243 通过+1 跳过；Api 366 / Blazor 71 / MafWfApi 65 / FileStorageApi 153 全部通过
- [x] 新增冒烟用例 `TreeNodeActionsDialog_TreeHost_Renders`（标题「节点操作」+ 六按钮集）
- [x] 浏览器回归：树内 设置修改→保存→自动重开宿主对话框→名称持久化→属性对话框 全链通过
- [x] 浏览器回归：搜索页 UnitTree 行 ⋯ 直开节点操作对话框；LineNode 行 ⋯ 直开线路节点宿主对话框
- [x] 浏览器回归：结果行树定位 / AI 综合图标仍在
- [x] 测试改动数据（货币→货币改）已恢复

## 已知未覆盖
- [ ] 裸节点/实体命中行 ⋯ → NodeAttributesDialog：开发库当前无 HostType=Node 裸节点且无实体命中，
      未做浏览器覆盖（该分支复用重构前 LineNodeActions.AttributesAsync 既有链路，NodeAttributesDialog
      本身已在树内「属性」按钮链路验证渲染正常）
- [ ] E2E 测试（TreeGraph.Blazor.E2E.Tests）需 AppHost 运行环境，不在本次门禁内
