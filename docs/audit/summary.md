# 业务页布局债排查汇总

- 日期：2026-10-06
- 视口：804×794（桌面；390px 窄屏验证待补）
- 工具：[audit.js](audit.js)（源文件）；运行时由 `TreeGraph.Blazor/wwwroot/audit.js` 托管（两者需保持同步）
- 方法：每页注入 `auditPage()`，采集 6 维度计数与命中明细

## 方法论修正记录（脚本 v2）

初版 4 类误报已修复：

| 误报 | 表现 | 修正 |
|---|---|---|
| AppBar 全宽 | 每页工具栏维度 +1 | 排除 `.mud-toolbar-appbar`，只判超出父容器 |
| 关闭的抽屉/弹层 | 详情页溢出 +68 | 排除 `[class*="closed"]` 抽屉与未开 Popover 子树 |
| 全宽输入框 | `/attributes` 固定宽度 +4 | 只报内联 `width/min-width` 或显式 min-width ≥70% 视口 |
| 响应式栅格项 | `/attributes` 窄网格 +4 | 带 `mud-grid-item-xs-*` 档位的不报 |
| 输入框内嵌小图标 | 每页触控 +1~3 | 排除 `.mud-input-adornment` 子树；组件默认尺寸单列计数 |

## 各页结果（v2 脚本，仅列非零项）

| 页面 | 横向溢出 | 固定宽度 | 窄网格 | 表格 | 工具栏 | 触控(非默认) |
|---|---|---|---|---|---|---|
| /inodes | 0 | **1** | 0 | 0 | 0 | 0 |
| /inodes/inode-seed-001 | 0 | 0 | 0 | 0 | 0 | 0 |
| /inodes/inode-seed-001/types/item | 0 | 0 | 0 | 0 | 0 | 0 |
| /inodes/inode-seed-001/types/item/history | 0 | 0 | 0 | 0 | 0 | 1 |
| /entities | 0 | 0 | 0 | 0 | 0 | 0 |
| /entities/item | 0 | 0 | 0 | 0 | 0 | 0 |
| /entities/item/item-seed-001 | 0 | 0 | 0 | 0 | 0 | 2 |
| /entities/item/item-seed-001/history | 0 | 0 | 0 | 0 | 0 | 1 |
| /attributes * | 0 | 0 | 0 | 0 | 0 | 2 |
| /metadata/attributes * | 0 | 0 | 0 | 0 | 0 | 3 |
| /metadata/composite-types * | 0 | 0 | 0 | 0 | 0 | 3 |
| /metadata/units | 0 | 0 | 0 | 0 | 0 | 0 |
| /metadata/entity-types | 0 | 0 | 0 | 0 | 0 | 0 |
| /metadata/option-sets | 0 | 0 | 0 | 0 | 0 | 0 |
| /metadata/custom-tables | 0 | 0 | 0 | 0 | 0 | 0 |
| /schema | 0 | 0 | 0 | 0 | 0 | 1 |
| /query | 0 | 0 | 0 | 0 | 0 | 1 |

\* `/attributes`、`/metadata/attributes`、`/metadata/composite-types` 的 fixedWidth/narrowGrid 命中系 v1 脚本误报，按 v2 规则复核归 0；触控命中为 v1 数据。

## 确认的问题

### ~~P-A~~ ✅ 已修复

**问题**：`/inodes` GUID 输入行原内联硬编码 `min-width: 480px; max-width: 640px`，390px 视口下必溢出。

**修复**（[InodeList.razor](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/NodeEav/Pages/Inodes/InodeList.razor)）：去掉 IsCompact 三元分支，GUID 输入框外套一层包装 div（flex item），宽度约束挂在包装 div 上：`flex: 1 1 480px; min-width: min(480px, 100%); max-width: 640px;`。注意 MudTextField 的 Style 落在内层 `.mud-input`，无法约束外层 `.mud-input-control` 的占位宽度，故必须用包装 div。

**回归验证**：`fixedWidth=0`，GUID 输入框 `minWidth=auto`、`maxWidth=100%`，无横向溢出。

### P-B（类级，触控目标）

多个页面存在 **26×26 的 `mud-icon-button-size-small` 图标按钮**（不在输入框 adornment 内）：
`/attributes`×2、`/metadata/attributes`×3、`/metadata/composite-types`×3、
`/entities/item/item-seed-001`×2、两个 history 页各 ×1、`/schema`×1、`/query`×1。
低于 44dp 触控标准。修复方向：逐个改 `Size.Medium`，或接受为桌面密集 UI 的既定取舍。

组件库默认触控不足（36px 标准按钮）各页另有 0–3 处，已单列计数，不算页面债。

## 待补验证

- [ ] 390×844 窄屏复跑（重点验证维度 1 召回率、`/inodes` GUID 行溢出复现）
- [ ] P-A 修复后复跑 `/inodes` 回归
