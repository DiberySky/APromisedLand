#!/usr/bin/env bash
# ============================================================
# 空间 E2E 验证脚本
#
# 用法：
#   BASE=http://localhost:5773 bash scripts/e2e-space-verify.sh
#
# 依赖：curl、jq
# ============================================================

set -euo pipefail

BASE="${BASE:-http://localhost:5773}"
CURL="curl -sk"
JQ="jq -r"

# 颜色
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# 请求体统一走 UTF-8 临时文件（--data-binary @file）：
# Windows 的 Git Bash 调用原生 curl.exe 时，内联中文参数会被损坏导致 400。
TMPD="$(mktemp -d)"
trap 'rm -rf "$TMPD"' EXIT

pass() { echo -e "${GREEN}✅ $*${NC}"; }
fail() { echo -e "${RED}❌ $*${NC}"; exit 1; }
info() { echo -e "${YELLOW}▶ $*${NC}"; }

# ============================================================
# 0. 健康检查
# ============================================================
info "健康检查"
$CURL "$BASE/health" >/dev/null || fail "服务未启动：$BASE"
pass "服务正常"

# ============================================================
# 1. 创建独立空间
# ============================================================
info "1. 创建独立空间"
printf '%s' '{"name":"E2E-独立空间","description":"脚本创建","entityType":"auto"}' > "$TMPD/space.json"
SPACE_JSON=$($CURL -X POST "$BASE/api/string-tree/spaces" \
  -H "Content-Type: application/json" \
  --data-binary @"$TMPD/space.json")

SPACE_ID=$(echo "$SPACE_JSON" | $JQ '.data.id')
SPACE_ET=$(echo "$SPACE_JSON" | $JQ '.data.entityType')

[[ -n "$SPACE_ID" && "$SPACE_ID" != "null" ]] || fail "空间创建失败：$SPACE_JSON"
[[ "$SPACE_ET" == "StringTreeNode:$SPACE_ID" ]] || fail "EntityType 不符合预期：$SPACE_ET"
pass "空间创建：$SPACE_ID（EntityType=$SPACE_ET）"

# ============================================================
# 2. 验证 EntityType 已注册
# ============================================================
info "2. 验证 EntityType 在 catalog 中已注册"
ET_LIST=$($CURL "$BASE/api/eav/entity-types")
FOUND=$(echo "$ET_LIST" | jq --arg et "$SPACE_ET" \
  '[.[] | select(.entityType == $et)] | length')
[[ "$FOUND" == "1" ]] || fail "EntityType 未注册到 catalog"
pass "EntityType 已注册"

# ============================================================
# 3. 为该 EntityType 添加属性
# ============================================================
info "3. 添加属性 brand（string，必填）"
cat > "$TMPD/attr1.json" <<EOF
{
  "entityType":"$SPACE_ET",
  "attributeName":"brand",
  "displayName":"品牌",
  "dataType":"string",
  "isRequired":true,
  "isSearchable":true,
  "isSortable":false,
  "displayOrder":0
}
EOF
ATTR1=$($CURL -X POST "$BASE/api/eav/metadata/attributes" \
  -H "Content-Type: application/json" \
  --data-binary @"$TMPD/attr1.json")
ATTR1_ID=$(echo "$ATTR1" | $JQ '.attributeId // .data.attributeId')
[[ -n "$ATTR1_ID" && "$ATTR1_ID" != "null" ]] || fail "属性创建失败：$ATTR1"
pass "属性 brand 创建：$ATTR1_ID"

info "   添加属性 price（decimal）"
cat > "$TMPD/attr2.json" <<EOF
{
  "entityType":"$SPACE_ET",
  "attributeName":"price",
  "displayName":"价格",
  "dataType":"decimal",
  "isRequired":false,
  "isSearchable":true,
  "isSortable":true,
  "displayOrder":1
}
EOF
$CURL -X POST "$BASE/api/eav/metadata/attributes" \
  -H "Content-Type: application/json" \
  --data-binary @"$TMPD/attr2.json" >/dev/null
pass "属性 price 创建"

# ============================================================
# 4. 在空间下创建子节点
# ============================================================
info "4. 创建树节点"
# entityType 必须传 "auto"：缺省时 DTO 默认值 "StringTreeNode" 会阻止服务端父节点继承分支
printf '%s' "{\"name\":\"手机\",\"parentId\":\"$SPACE_ID\",\"entityType\":\"auto\"}" > "$TMPD/node.json"
NODE_JSON=$($CURL -X POST "$BASE/api/string-tree/nodes" \
  -H "Content-Type: application/json" \
  --data-binary @"$TMPD/node.json")
NODE_ID=$(echo "$NODE_JSON" | $JQ '.data.id')
[[ -n "$NODE_ID" && "$NODE_ID" != "null" ]] || fail "节点创建失败：$NODE_JSON"
pass "节点创建：$NODE_ID"

# 验证子节点继承了空间的 EntityType
NODE_ET=$($CURL "$BASE/api/string-tree/nodes/$NODE_ID" | $JQ '.data.entityType')
[[ "$NODE_ET" == "$SPACE_ET" ]] || fail "子节点 EntityType 未继承：$NODE_ET ≠ $SPACE_ET"
pass "子节点 EntityType 继承正确"

# ============================================================
# 5. 写节点属性
# ============================================================
info "5. 写入节点 EAV 属性"
printf '%s' '{"brand":"华为","price":4999}' > "$TMPD/values.json"
WRITE_RESP=$($CURL -X PUT "$BASE/api/eav/$SPACE_ET/entities/$NODE_ID" \
  -H "Content-Type: application/json" \
  --data-binary @"$TMPD/values.json")
HTTP_CODE=$(echo "$WRITE_RESP" | head -c 1)
[[ "$HTTP_CODE" == "{" || -z "$WRITE_RESP" ]] || fail "属性写入失败：$WRITE_RESP"
pass "属性写入成功"

# ============================================================
# 6. 读回属性
# ============================================================
info "6. 读回节点属性"
READ=$($CURL "$BASE/api/eav/$SPACE_ET/entities/$NODE_ID")
BRAND=$(echo "$READ" | $JQ '.properties.brand')
# decimal 回读带完整标度尾零（如 4999.000000000000000），按数值比较而非字符串
PRICE=$(echo "$READ" | $JQ '.properties.price')
PRICE_OK=$(echo "$READ" | jq '.properties.price == 4999')
[[ "$BRAND" == "华为" ]] || fail "brand 回读不符：$BRAND"
[[ "$PRICE_OK" == "true" ]] || fail "price 回读不符：$PRICE"
pass "属性回读正确：brand=$BRAND, price=$PRICE"

# ============================================================
# 7. 属性过滤查询
# ============================================================
info "7. 按属性过滤查询"
cat > "$TMPD/query.json" <<EOF
{
  "entityType":"$SPACE_ET",
  "filters":[{"attributeName":"brand","operator":"eq","value":"华为"}],
  "page":1,"pageSize":100
}
EOF
QUERY=$($CURL -X POST "$BASE/api/eav/$SPACE_ET/entities/query" \
  -H "Content-Type: application/json" \
  --data-binary @"$TMPD/query.json")
HIT_COUNT=$(echo "$QUERY" | jq '.items | length')
[[ "$HIT_COUNT" -ge "1" ]] || fail "过滤查询未命中"
pass "过滤查询命中 $HIT_COUNT 条"

# ============================================================
# 8. 空间列表验证
# ============================================================
info "8. 验证空间出现在列表中"
LIST=$($CURL "$BASE/api/string-tree/spaces")
IN_LIST=$(echo "$LIST" | jq --arg id "$SPACE_ID" \
  '[.data[] | select(.id == $id)] | length')
[[ "$IN_LIST" == "1" ]] || fail "空间未出现在列表中"
pass "空间在列表中"

# ============================================================
# 9. 删除空间
# ============================================================
info "9. 删除空间（含子树 + EAV）"
DEL=$($CURL -X DELETE "$BASE/api/string-tree/spaces/$SPACE_ID")
DEL_OK=$(echo "$DEL" | $JQ '.data')
[[ "$DEL_OK" == "true" ]] || fail "删除失败：$DEL"
pass "空间已删除"

# 验证节点已消失
NODE_CHECK=$($CURL -o /dev/null -w "%{http_code}" \
  "$BASE/api/string-tree/nodes/$NODE_ID")
[[ "$NODE_CHECK" == "404" ]] || fail "节点未删除，HTTP=$NODE_CHECK"
pass "节点已删除"

# 验证属性已清空
ATTR_CHECK=$($CURL "$BASE/api/eav/$SPACE_ET/entities/$NODE_ID")
ATTR_COUNT=$(echo "$ATTR_CHECK" | jq '.properties | length' 2>/dev/null || echo "0")
[[ "$ATTR_COUNT" == "0" || "$ATTR_COUNT" == "null" ]] || fail "EAV 属性残留：$ATTR_CHECK"
pass "EAV 属性已清空"

# ============================================================
# 10. 验证 EntityType 已软删（无其他引用）
# ============================================================
info "10. 验证 EntityType 软删"
ET_AFTER=$($CURL "$BASE/api/eav/entity-types")
ET_STILL=$(echo "$ET_AFTER" | jq --arg et "$SPACE_ET" \
  '[.[] | select(.entityType == $et)] | length')
[[ "$ET_STILL" == "0" ]] || fail "EntityType 未软删（仍可见）"
pass "EntityType 已软删"

# ============================================================
# 完成
# ============================================================
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✅ E2E 验证全部通过${NC}"
echo -e "${GREEN}========================================${NC}"
