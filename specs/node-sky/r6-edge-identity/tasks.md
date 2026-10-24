# R6 Tasks

- [x] Task 1: 数据模型与迁移
  - [x] 1.1 `TreeGraph.Shared/NodeEavSky/EavDataTypes.cs`：新增 `Relation = "relation"` 并加入 `All`
  - [x] 1.2 `AttributeDefinition` 增加 `RelationType`（`varchar(100)`，可空）；`ConfigureAttributeCatalog` 映射 `relation_type`
  - [x] 1.3 新增 `NodeEavSky/Entities/RelationAttributeValue.cs`：`ValueId / EntityId / EntityType / AttributeId / RelationType / ToNodeId / Name / CreatedAt / UpdatedAt` + `Attribute` 导航
  - [x] 1.4 `TreeGraphDbContext`：新增 `DbSet<RelationAttributeValue>`、`ConfigureRelationAttributeValues`（两个唯一键 `uq_rav_entity_attr`、`uq_rav_type_endpoints`，索引 `ix_rav_entity`、`ix_rav_to_node`，`attribute_id` FK Restrict、`to_node_id` FK `nodes` Restrict）
  - [x] 1.5 开发期策略：drop 库 → 删除历史迁移 → 重新生成单一 `InitialCreate` → 补回 `CreateAuditPartitionTable` / `DropAuditPartitionTable` 调用
  - [x] 1.6 `dotnet ef migrations script` 确认新表、两个唯一键、两个索引与分区表 DDL 均在

- [x] Task 2: 属性定义与 relation_type
  - [x] 2.1 `MetadataDtos`：`CreateAttributeRequest` 增加 `RelationType`；`AttributeDetailDto` 增加 `RelationType`（`UpdateAttributeRequest` 不加，不可改）
  - [x] 2.2 新增 `RelationNaming`（放 `NodeEavSky/Services` 或 `GraphSyncSky`）：`Resolve(def/req)` + `Validate`，规则为转小写、`^[a-z][a-z0-9_]*$`、≤100、拒绝 `parent_of/line_contains/connects_to/line_segment`
  - [x] 2.3 回退链：显式 `relationType` → 用户指定的 `AttributeName` 驼峰转 snake → `attr_` 自动名则 400（错误文案：「关系属性必须填写 relation_type，或指定英文属性名」）
  - [x] 2.4 `EavMetadataController.CreateAttribute`：`dataType=relation` 时解析并写入 `RelationType`；非 relation 却填 `relationType` → 400；relation 的 `isSearchable/isSortable` 忽略（落库 false）
  - [x] 2.5 `EavMetadataController.UpdateAttribute`：`relation_type` 不可改（请求无该字段，若强行传入忽略）；`ToAttributeDetailDto` 透出 `RelationType`
  - [x] 2.6 元数据前端：`dataType` 增加「关系」选项；`dataType=relation` 时显示 `relation_type` 输入（复用 `NamingTextField`，`NameCase="snake"`、`SourceText=DisplayName`），占位「如 belongs_to；留空则按英文属性名推导」，HelperText 注明字符集、长度与保留名
  - [x] 2.7 属性列表/详情展示 `relation_type` 与「图边类型」

- [x] Task 3: 关系值读写
  - [x] 3.1 新增 `TreeGraph.Shared/NodeEavSky/Dtos/RelationDtos.cs`：`EavRelationValue { ToNodeId, Name? }`（请求）与读取体 `{ ValueId, ToNodeId, Name? }`
  - [x] 3.2 `EavRequestBodyConverter`：增加 `EavDataTypes.Relation => ParseRelation(elem)`；缺 `toNodeId` 或非字符串 → `ArgumentException`
  - [x] 3.3 `EavValidationService.ValidateBaseValue`：增加 `ValidateRelation`（`toNodeId` 非空、`name` ≤200；不做 DB 查询）
  - [x] 3.4 `EavWriteService`：新增 `ApplyRelation`（clone `ApplyLocation`）：值为 null → 删除 + 审计；无旧值 → Insert（`RelationType` 取 `def.RelationType`，为空则抛错）；有旧值 → 更新目标/名称 + 审计；命中 `uq_rav_type_endpoints` → 409 文案「同一图边类型下，该起点已指向该终点」
  - [x] 3.5 `EavWriteService.SaveCoreAsync`：加载 `existingRelationMap`（按 `entity_id + entity_type + attribute_id`）；显式分支与 `fullReplace` 删除分支都要处理 relation
  - [x] 3.6 写入前批量校验 `to_node_id` 存在于 `nodes`（缺失 → 400「目标节点不存在：{ids}」），FK 兜底
  - [x] 3.7 乐观锁：`SaveCoreAsync` 与 `EavReadService` 的 `UpdatedAt` 上界均合并 `relation_attribute_values`
  - [x] 3.8 `EavReadService.LoadAsync` / `LoadBatchAsync`：加载 relation 行，`def.DataType == Relation` 时 `SetProperty(name, new { valueId, toNodeId, name })`
  - [x] 3.9 `EavController` 的写入转换分支加 `Relation => ParseRelation`；确认 `Table` 互斥逻辑不误伤 relation
  - [x] 3.10 `SubtreeService.FormatAttributeAsync`：relation 分支显示 `name ?? toNodeId`（必要时批量解析节点名）

- [x] Task 4: 删除守卫
  - [x] 4.1 `EavMetadataController.DeleteAttribute`：增加 `RelationAttributeValues.AnyAsync(r => r.AttributeId == id)`，与现有值检查同文案拒绝
  - [x] 4.2 `NodeController.Delete`：同一事务内先检测后删除；新增 `AttributeValues` / `LocationAttributeValues`（有值 → 409「该节点仍有 EAV 属性数据（N 项）…」）、`RelationAttributeValues` 作为起点（409）、`to_node_id` 指向该节点（409「该节点仍被 N 条关系引用…」）
  - [x] 4.3 删除按钮保持常显；前端把 409 的 `error` 文案用 Snackbar 展示（含计数）

- [x] Task 5: 事件与图投影
  - [x] 5.1 新增 `TreeGraph.Shared/Messaging/RelationChangedEvent.cs`：`{ ValueId, AttributeId, RelationType, FromNodeId, ToNodeId, Name?, Kind }` + `RelationChangeKind`
  - [x] 5.2 `MessagingExtensions`：publish/listen `treegraph.relation-changed`
  - [x] 5.3 `EavWriteService` 注入 `IEventPublisher`；在 `SaveChanges` **成功后**逐条发布（Insert/Update/Delete），避免事务内发送
  - [x] 5.4 `GraphSchemaInitializer`：把「按 `edge_instances.type_name` 动态建边」改为「按 `attribute_catalog` 中 `data_type='relation' and is_deleted=false` 的 `relation_type`」，建边列 `value_id string / attribute_id string / name string`
  - [x] 5.5 `GraphSynchronizer`：新增 `SyncRelationAsync(RelationChangedEvent)`（upsert）与 `DeleteRelationAsync`（按 `relation_type + from + to` 删边），边属性 `value_id/attribute_id/name`
  - [x] 5.6 全量重建与 `SyncCategory` bulk：追加 `relation_attribute_values` 投影（按 `relation_type` 分组，先确保边类型存在），统计计入 `SemanticEdges`
  - [x] 5.7 新增 `RelationGraphSyncConsumer`（去重键 `graph-sync-relation:{ValueId}`，窗口取 `DedupWindowSeconds`）并注册
  - [x] 5.8 删除 `EdgeChangedEvent` / `EdgeChangeKind` 与旧 `EdgeGraphSyncConsumer` 分支

- [x] Task 6: EdgeInstance 下线
  - [x] 6.1 删除 `EdgeSky/Entities/EdgeInstance.cs`、`EdgeSky/Services/EdgeInstanceService.cs`、`EdgeSky/Controllers/EdgeController.cs`、`DbSet<EdgeInstance>`、`ConfigureEdges`
  - [x] 6.2 删除 `TreeGraph.Shared/EdgeSky/**`（`EdgeDtos`、`EdgeVocabulary`）并清理引用
  - [x] 6.3 `GraphSynchronizer`：删除 `SyncEdgeAsync`、`DeleteEdgeByPayloadAsync`、`EdgeProps`、`ResolveNodeAsync` 的 EdgeInstance 分支及相关注释
  - [x] 6.4 `NodesRegistryGuard`：删除 EdgeInstance 相关分支/注释（`GetNodeNamesAsync` 的边名投影一并移除）
  - [x] 6.5 删除 `TreeGraph.Blazor.Shared/EdgeSky/**`（`EdgeDialog`、`EdgeInstances`、`EdgeRelationPanel`、`EdgeApiClient`）及页面入口
  - [x] 6.6 全仓 `grep -i edgeinstance` 与 `grep -i "api/eav/edges"` 清零（迁移文件除外）

- [x] Task 7: 前端关系字段
  - [x] 7.1 新增 `NodeEavSky/Components/FieldRenderers/RelationField.razor`（clone `LocationField` 的形态：主输入 + 尾部动作）
  - [x] 7.2 目标节点选择：搜索框调 `GET api/node?search=`，选中后显示 `名称`；允许清空
  - [x] 7.3 `name` 可选输入（≤200，占位「如 主供 / 备用」）
  - [x] 7.4 `DynamicForm.razor` 增加 `EavDataTypes.Relation` 分支与 `GetRelation` / `SetValue` 适配；只读模式展示 `名称（→ 目标名）`
  - [x] 7.5 关系值读取体 `{ valueId, toNodeId, name }` 与提交体 `{ toNodeId, name }` 对齐

- [x] Task 8: 测试
  - [x] 8.1 元数据：显式 relation_type、驼峰回退、自动名 400、非法字符/大写/`_` 开头/超长、内置类型 400、非 relation 填 relation_type 400、更新不可改
  - [x] 8.2 值：写入/读取/删除、单值覆盖、`toNodeId` 不存在 400、`(relation_type, from, to)` 冲突 409、同类型不同端点共存、自环、乐观锁、审计 Insert/Update/Delete
  - [x] 8.3 删除守卫：属性有值拒绝、node 三类（自身属性值、作为起点、被指向）409
  - [x] 8.4 图投影：schema 按 `relation_type` 动态建边、事件发布与消费、upsert/delete、全量重建载荷含关系边
  - [x] 8.5 删除/改写 `EdgeTests`、`EdgeHostTests` 及图同步旧边用例
  - [x] 8.6 前端：`RelationField` 渲染与提交、`DynamicForm` 含 relation 时的值往返
  - [x] 8.7 跑 Api 测试，数量不低于 330

- [x] Task 9: 验证与收尾
  - [x] 9.1 `dotnet build APromisedLand.sln` 0 错误
  - [x] 9.2 五套测试全绿（Api ≥330、Blazor.Shared ≥235+1skip、MafWfApi 65、FileStorageApi 153、Blazor 71）
  - [x] 9.3 写 `review.md` 并勾选本文件

# Task Dependencies

- Task 2、Task 3 依赖 Task 1
- Task 4 依赖 Task 1、Task 3
- Task 5 依赖 Task 1、Task 3
- Task 6 依赖 Task 5（先切断旧事件与同步再删表）
- Task 7 依赖 Task 2、Task 3
- Task 8 依赖 Task 2、Task 3、Task 4、Task 5、Task 6、Task 7
- Task 9 依赖全部
