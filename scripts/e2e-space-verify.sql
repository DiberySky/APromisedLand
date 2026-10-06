-- ============================================================
-- 空间 E2E 验证：数据库核对
-- 用法（本机库 = TreeGraphDb，容器 127.0.0.1:8433，账号 postgres/postgres）：
--   psql -h localhost -p 8433 -U postgres -d TreeGraphDb -f scripts/e2e-space-verify.sql
-- 无本地 psql 时：
--   docker exec -i Postgres-57f7d103 psql -U postgres -d TreeGraphDb < scripts/e2e-space-verify.sql
-- ============================================================

\echo '=== 1. 空间节点 ==='
SELECT id, name, entity_type, parent_id
FROM string_tree_sky_nodes
WHERE parent_id IS NULL
ORDER BY created_at DESC
LIMIT 10;

\echo '=== 2. 独立 EntityType 注册状态 ==='
SELECT entity_type, display_name, is_deleted, created_at
FROM entity_type_catalog
WHERE entity_type LIKE 'StringTreeNode:%'
ORDER BY created_at DESC
LIMIT 10;

\echo '=== 3. 每个 EntityType 的属性数 ==='
SELECT entity_type, COUNT(*) AS attr_count
FROM attribute_catalog
WHERE is_deleted = false
GROUP BY entity_type
ORDER BY attr_count DESC;

\echo '=== 4. 孤立 EntityType 检测（有类型无空间） ==='
SELECT et.entity_type, et.display_name
FROM entity_type_catalog et
LEFT JOIN string_tree_sky_nodes n
  ON n.entity_type = et.entity_type AND n.parent_id IS NULL
WHERE et.entity_type LIKE 'StringTreeNode:%'
  AND et.is_deleted = false
  AND n.id IS NULL;

\echo '=== 5. 孤立 EAV 数据检测（有值无节点） ==='
SELECT av.entity_type, COUNT(*) AS orphan_values
FROM attribute_values av
LEFT JOIN string_tree_sky_nodes n ON n.id = av.entity_id
WHERE n.id IS NULL
GROUP BY av.entity_type;

\echo '=== 6. 孤立自定义表行检测 ==='
SELECT ctr.parent_entity_type, COUNT(*) AS orphan_rows
FROM custom_table_rows ctr
LEFT JOIN string_tree_sky_nodes n ON n.id = ctr.parent_entity_id
WHERE n.id IS NULL
GROUP BY ctr.parent_entity_type;

\echo '=== 7. 每个空间的节点数 ==='
SELECT
  root.name AS space_name,
  root.entity_type,
  COUNT(n.id) - 1 AS descendant_count
FROM string_tree_sky_nodes root
LEFT JOIN string_tree_sky_nodes n
  ON n.id = root.id OR n.parent_id = root.id
WHERE root.parent_id IS NULL
GROUP BY root.id, root.name, root.entity_type
ORDER BY descendant_count DESC;
