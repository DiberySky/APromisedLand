-- =====================================================================
-- 开发期测试数据恢复脚本（安全网）
--
-- 用途：Aspire 重建 Postgres 容器 / 换卷 / 换机 / 他人接手后，
--       一键恢复本会话用于验证 D1（裸数字单位包装）的冒烟夹具。
--
-- 前置：
--   1. treegrapheavapi 至少成功启动过一次
--      （UnitSeedService 已 seed 固定 GUID 单位；EavSeeder 已播种 Product 元数据）
--   2. 目标库 TreeGraphDb
--
-- 幂等：可重复执行。
--   - specs.weight 绑定用 UPDATE（按 类型名+字段名 定位，不依赖固定 field_id）
--   - 两个冒烟实体用 INSERT ... ON CONFLICT DO UPDATE
--     （依赖唯一索引 uq_av_entity_attr(entity_id, entity_type, attribute_id)）
--
-- 用法（PowerShell，容器名每次重建会变，用 --filter 取当前名）：
--   $c = docker ps --filter "name=Postgres" --format "{{.Names}}" | Select-Object -First 1
--   Get-Content "docs\dev-seed.sql" -Raw |
--       docker exec -i -e PGPASSWORD='<密码>' $c psql -U postgres -d TreeGraphDb
-- =====================================================================

BEGIN;

-- 1. specs.weight（Specs 组合类型的 weight 字段）绑定固定基准单位 kg -------------------
--    固定 kg GUID 来自 UnitSeedService（weight 分类基准单位）
UPDATE composite_field_definitions
SET unit_id = 'e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e'
WHERE field_name = 'weight'
  AND composite_type_id = (
      SELECT composite_type_id
      FROM composite_type_definitions
      WHERE entity_type = 'Product' AND type_name = 'Specs'
  );

-- 2. 冒烟实体 990001：旧数据形态——裸数字（无 unitId） -------------------------------
--    验证点：GET 时 D1 修复应把 7 包装为 {"value":7,"unitId":<kg>}
INSERT INTO attribute_values
    (entity_id, entity_type, attribute_id, value_jsonb, created_at, updated_at)
VALUES
    (990001, 'Product', 4,
     '{"color":"_d1smoke_bare","weight":7}'::jsonb, NOW(), NOW())
ON CONFLICT (entity_id, entity_type, attribute_id) DO UPDATE
SET value_jsonb = EXCLUDED.value_jsonb,
    updated_at   = NOW();

-- 3. 冒烟实体 990002：对象形态 + 非基准单位 g（固定 g GUID） -------------------------
--    验证点：GET 默认归一化为 kg {"value":1,"unitId":<kg>}；
--           GET ?unit=original 还原为 {"value":1000,"unitId":<g>}
INSERT INTO attribute_values
    (entity_id, entity_type, attribute_id, value_jsonb, created_at, updated_at)
VALUES
    (990002, 'Product', 4,
     '{"color":"_d1smoke_obj","weight":{"value":1,"unitId":"f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f"}}'::jsonb,
     NOW(), NOW())
ON CONFLICT (entity_id, entity_type, attribute_id) DO UPDATE
SET value_jsonb = EXCLUDED.value_jsonb,
    updated_at   = NOW();

COMMIT;

-- =====================================================================
-- 执行后核对（应为每个实体返回 1 行）：
--   SELECT entity_id, value_jsonb->'weight' AS weight
--   FROM attribute_values WHERE entity_id IN (990001,990002) ORDER BY entity_id;
--
-- 在线三形态验证：
--   GET http://localhost:5773/api/eav/Product/entities/990001
--     -> weight = {"value":7,"unitId":"e8c9d0e1-...(kg)"}
--   GET http://localhost:5773/api/eav/Product/entities/990002
--     -> weight = {"value":1,"unitId":"e8c9d0e1-...(kg)"}
--   GET http://localhost:5773/api/eav/Product/entities/990002?unit=original
--     -> weight = {"value":1000,"unitId":"f9d0e1f2-...(g)"}
--
-- 清理（如需移除冒烟数据）：
--   DELETE FROM attribute_values WHERE entity_id IN (990001,990002);
--   （如需同时解绑）UPDATE composite_field_definitions SET unit_id = NULL
--     WHERE field_name='weight' AND composite_type_id=
--       (SELECT composite_type_id FROM composite_type_definitions
--        WHERE entity_type='Product' AND type_name='Specs');
-- =====================================================================
