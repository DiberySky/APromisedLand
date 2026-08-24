// using System.Security.Cryptography;
// using System.Text;
// using System.Text.Json;
// using APromisedLand.Api.Data;
// using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
// using APromisedLand.Shared.DiberyTree.Attributes.Enums;
// using APromisedLand.Shared.DiberyTree.Attributes.Models;
// using APromisedLand.Shared.DiberyTree.Attributes.Validation;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Caching.Memory;
//
// namespace APromisedLand.Api.DiberyTree.Services;
//
// /// <summary>
// /// 动态表数据（行实例 + 列值）的业务服务。
// /// <para>行实例 = <see cref="APromisedLand.Shared.DiberyTree.Attributes.Models.TableRowAttributeValue"/>（AttributeDefinitionId=表定义, NodeId=表定义）；
// /// 列值复用各 Typed AttributeValue 表（NodeId=行实例 Id, AttributeDefinitionId=列定义）。</para>
// /// 列值经 <see cref="ValueValidator"/> 校验后写入对应 typed value 表。
// /// </summary>
// public class AttributeTableValueService(DiberyDbContext db, IMemoryCache cache)
// {
//     // 幂等去重：请求指纹缓存的时间窗口（秒）
//     private const int TabelDedupWindowSeconds = 30;
//     private const string TableDedupCachePrefix = "AttrTbl_AddRow_Dedup_";
//
//     // ---------- 新增行 ----------
//
//     /// <summary>向指定表定义添加一行数据（带幂等去重）。</summary>
//     /// <returns>(行实例 Id, 错误信息, 是否为重复请求)。当 Duplicated=true 时表示请求已在 30 秒窗口内处理过，已直接返回第一次的结果。</returns>
//     public async Task<(string? RowId, string? Error, bool Duplicated)> AddRowAsync(string nodeId, string tableId,
//         string tableDefId, Dictionary<string, JsonElement> values)
//     {
//         // ========== 第一步：基于请求内容计算 SHA256 指纹，做幂等去重 ==========
//         var fingerprint = ComputeAddRowFingerprint(nodeId, tableId, tableDefId, values);
//         var cacheKey = TableDedupCachePrefix + fingerprint;
//
//         // 如果 30 秒窗口内已经处理过完全相同的请求，直接返回第一次结果，不做插入
//         if (cache.TryGetValue<(string? RowId, string? Error)>(cacheKey, out var cached))
//         {
//             return (cached.RowId, cached.Error, Duplicated: true);
//         }
//
//         // 占位：防止请求真正落库之前又进来同一个请求（线程内并发 + 多实例并发窗口保护）
//         var placeholder = (RowId: (string?)null, Error: (string?)null);
//         cache.Set(cacheKey, placeholder, TimeSpan.FromSeconds(TabelDedupWindowSeconds));
//
//         // ========== 第二步：执行业务插入逻辑 ==========
//         var (rowId, error) = await AddRowInternalAsync(nodeId, tableId, tableDefId, values);
//
//         // 将实际结果写回缓存，覆盖占位值，后续相同请求直接拿到真实 RowId/Error
//         var finalResult = (RowId: rowId, Error: error);
//         cache.Set(cacheKey, finalResult, TimeSpan.FromSeconds(TabelDedupWindowSeconds));
//
//         return (rowId, error, Duplicated: false);
//     }
//
//     /// <summary>计算 AddRow 请求的 SHA256 指纹。对相同 nodeId+tableId+tableDefId+相同列值（含顺序）得到相同指纹。</summary>
//     private static string ComputeAddRowFingerprint(
//         string nodeId, string tableId, string tableDefId, Dictionary<string, JsonElement> values)
//     {
//         // 使用有序键序列，确保相同列值集合（即便字典遍历顺序不同）也能生成一致指纹
//         var sb = new StringBuilder();
//         sb.Append("nodeId=").Append(nodeId).Append('|');
//         sb.Append("tableId=").Append(tableId).Append('|');
//         sb.Append("tableDefId=").Append(tableDefId).Append('|');
//
//         foreach (var kv in values.OrderBy(k => k.Key, StringComparer.Ordinal))
//         {
//             sb.Append(kv.Key).Append('=');
//             // JsonElement 的 GetRawText() 输出稳定，适合指纹
//             sb.Append(kv.Value.ValueKind == JsonValueKind.Undefined ? "" : kv.Value.GetRawText());
//             sb.Append(';');
//         }
//
//         var raw = Encoding.UTF8.GetBytes(sb.ToString());
//         var hash = SHA256.HashData(raw);
//         return Convert.ToBase64String(hash);
//     }
//
//     /// <summary>实际的行插入逻辑（不含去重）。返回 (行实例 Id, 错误信息)。</summary>
//     private async Task<(string? RowId, string? Error)> AddRowInternalAsync(string nodeId, string tableId,
//         string tableDefId, Dictionary<string, JsonElement> values)
//     {
//         // 1. 获取表定义（不再 Include AttributeType）
//         var tableDef = await db.AttributeDefinitions
//             .FirstOrDefaultAsync(d => d.Id == tableDefId);
//         if (tableDef == null) return (null, $"表定义 '{tableDefId}' 不存在");
//
//         // 通过映射判断是否为表格类型
//         if (!AttributeTypeMapping.IdToEnum.TryGetValue(tableDef.AttributeTypeId, out var tableType) || tableType != AttributeTypeEnum.表格)
//             return (null, $"'{tableDefId}' 不是表定义");
//         // 同时检查是否为表定义（ParentId 为 null 表示是表定义，而非列定义）
//         if (tableDef.ParentId != null)
//             return (null, $"'{tableDefId}' 不是表定义（有父级）");
//
//         // 2. 获取该表的所有列定义（不再 Include AttributeType）
//         var columns = await db.AttributeDefinitions
//             .Where(d => d.ParentId == tableDefId)
//             .OrderBy(d => d.Order)
//             .ToListAsync();
//         var colMap = columns.ToDictionary(c => c.Id);
//
//         // 3. 校验：列 Id 必须属于该表；列不可为「表」类型
//         foreach (var kv in values)
//         {
//             if (!colMap.TryGetValue(kv.Key, out var colDef))
//                 return (null, $"列定义 '{kv.Key}' 不属于表 '{tableDef.Name}'");
//
//             // 通过映射检查列的类型是否为表格
//             if (AttributeTypeMapping.IdToEnum.TryGetValue(colDef.AttributeTypeId, out var colType) && colType == AttributeTypeEnum.表格)
//                 return (null, "列不可为表类型");
//         }
//
//         // 4. 生成行 ID 和行号
//         var rowId = Guid.NewGuid().ToString();
//         var rowNo = await db.TableRowAttributeValues.CountAsync(r => r.AttributeDefinitionId == tableDefId) + 1;
//
//         // 5. 添加行实例
//         db.TableRowAttributeValues.Add(new TableRowAttributeValue
//         {
//             Id = rowId,
//             NodeId = nodeId,
//             AttributeDefinitionId = tableDefId,
//             TableId = tableId,
//             RowNo = rowNo
//         });
//
//         // 6. 写入各列值（NodeId = 行实例 Id）
//         foreach (var kv in values)
//         {
//             var colDef = colMap[kv.Key];
//             var (ok, err, entity) = ValueValidator.ValidateAndBuild(colDef, kv.Value, rowId);
//             if (!ok || entity is null) return (null, $"列 '{colDef.Name}': {err}");
//             AddValueEntity(entity);
//         }
//
//         await db.SaveChangesAsync();
//
//         return (rowId, null);
//     }
//
//     // ---------- 查询行 ----------
//
//     /// <summary>列出指定表的所有行实例（含各列值），按 RowNo 排序。</summary>
//     public async Task<List<TableRowDto>> ListRowsAsync(string tableDefId)
//     {
//         var rows = await db.TableRowAttributeValues.AsNoTracking()
//             .Where(r => r.AttributeDefinitionId == tableDefId)
//             .OrderBy(r => r.RowNo).ToListAsync();
//         if (rows.Count == 0) return new();
//
//         var cells = await LoadCellsAsync(rows.Select(r => r.Id));
//         var colNames = await db.AttributeDefinitions.AsNoTracking()
//             .Where(d => d.ParentId == tableDefId)
//             .Select(d => new { d.Id, d.Name })
//             .ToDictionaryAsync(d => d.Id);
//
//         return rows.Select(r => new TableRowDto
//         {
//             RowId = r.Id,
//             RowNo = r.RowNo ?? 0,
//             Values = cells.TryGetValue(r.Id, out var list)
//                 ? list.Select(c => new TableCellDto
//                 {
//                     ColumnId = c.AttributeDefinitionId,
//                     ColumnName = colNames.TryGetValue(c.AttributeDefinitionId, out var n) ? n.Name : null,
//                     Value = GetRawValue(c)
//                 }).ToList()
//                 : new()
//         }).ToList();
//     }
//
//     /// <summary>取单行实例（含各列值）。</summary>
//     public async Task<TableRowDto?> GetRowAsync(string rowId)
//     {
//         var row = await db.TableRowAttributeValues.AsNoTracking()
//             .FirstOrDefaultAsync(r => r.Id == rowId);
//         if (row is null) return null;
//
//         var cells = await LoadCellsAsync(new[] { rowId });
//         var colNames = await db.AttributeDefinitions.AsNoTracking()
//             .Where(d => d.ParentId == row.AttributeDefinitionId)
//             .Select(d => new { d.Id, d.Name })
//             .ToDictionaryAsync(d => d.Id);
//
//         var list = cells.TryGetValue(rowId, out var l) ? l : new();
//         return new TableRowDto
//         {
//             RowId = row.Id,
//             RowNo = row.RowNo ?? 0,
//             Values = list.Select(c => new TableCellDto
//             {
//                 ColumnId = c.AttributeDefinitionId,
//                 ColumnName = colNames.TryGetValue(c.AttributeDefinitionId, out var n) ? n.Name : null,
//                 Value = GetRawValue(c)
//             }).ToList()
//         };
//     }
//
//     // ---------- 更新行 ----------
//
//     /// <summary>更新某行实例的列值（先删旧列值，再重建）。</summary>
//     public async Task<string?> UpdateRowAsync(string rowId, Dictionary<string, JsonElement> values)
//     {
//         var row = await db.TableRowAttributeValues.FindAsync(rowId);
//         if (row is null) return $"行 '{rowId}' 不存在";
//
//         var columns = await db.AttributeDefinitions
//             .Where(d => d.ParentId == row.AttributeDefinitionId)
//             .ToListAsync();
//         var colMap = columns.ToDictionary(c => c.Id);
//         foreach (var kv in values)
//             if (!colMap.ContainsKey(kv.Key))
//                 return $"列 '{kv.Key}' 不属于该表";
//
//         DeleteCells(rowId);
//
//         foreach (var kv in values)
//         {
//             var colDef = colMap[kv.Key];
//             var (ok, err, entity) = ValueValidator.ValidateAndBuild(colDef, kv.Value, rowId);
//             if (!ok || entity is null) return $"列 '{colDef.Name}': {err}";
//             AddValueEntity(entity);
//         }
//
//         await db.SaveChangesAsync();
//         return null;
//     }
//
//     // ---------- 删除行 ----------
//
//     /// <summary>删除行实例及其所有列值。</summary>
//     public async Task<string?> DeleteRowAsync(string rowId)
//     {
//         var row = await db.TableRowAttributeValues.FindAsync(rowId);
//         if (row is null) return $"行 '{rowId}' 不存在";
//
//         DeleteCells(rowId);
//         db.TableRowAttributeValues.Remove(row);
//         await db.SaveChangesAsync();
//         return null;
//     }
//
//     // ---------- 辅助：批量加载列值（按行实例 Id 分组）----------
//     private async Task<Dictionary<string, List<AttributeValueBase>>> LoadCellsAsync(IEnumerable<string> rowIds)
//     {
//         var ids = rowIds.ToList();
//         var map = ids.ToDictionary(id => id, _ => new List<AttributeValueBase>());
//         await AddCells(db.TextAttributeValues, ids, map);
//         await AddCells(db.DecimalAttributeValues, ids, map);
//         await AddCells(db.IntegerAttributeValues, ids, map);
//         await AddCells(db.DateAttributeValues, ids, map);
//         await AddCells(db.TimeAttributeValues, ids, map);
//         await AddCells(db.DateTimeAttributeValues, ids, map);
//         await AddCells(db.FileAttributeValues, ids, map);
//         await AddCells(db.LocationAttributeValues, ids, map);
//         await AddCells(db.TableAttributeValues, ids, map);
//         return map;
//     }
//
//     private async Task AddCells<T>(IQueryable<T> set, List<string> ids, Dictionary<string, List<AttributeValueBase>> map)
//         where T : AttributeValueBase
//     {
//         var cells = await set.AsNoTracking().Where(c => ids.Contains(c.NodeId)).ToListAsync();
//         foreach (var c in cells)
//             if (map.TryGetValue(c.NodeId, out var list)) list.Add(c);
//     }
//
//     // ---------- 辅助：删除某行所有列值 ----------
//     private void DeleteCells(string rowId)
//     {
//         db.TextAttributeValues.RemoveRange(db.TextAttributeValues.Where(c => c.NodeId == rowId));
//         db.DecimalAttributeValues.RemoveRange(db.DecimalAttributeValues.Where(c => c.NodeId == rowId));
//         db.IntegerAttributeValues.RemoveRange(db.IntegerAttributeValues.Where(c => c.NodeId == rowId));
//         db.DateAttributeValues.RemoveRange(db.DateAttributeValues.Where(c => c.NodeId == rowId));
//         db.TimeAttributeValues.RemoveRange(db.TimeAttributeValues.Where(c => c.NodeId == rowId));
//         db.DateTimeAttributeValues.RemoveRange(db.DateTimeAttributeValues.Where(c => c.NodeId == rowId));
//         db.FileAttributeValues.RemoveRange(db.FileAttributeValues.Where(c => c.NodeId == rowId));
//         db.LocationAttributeValues.RemoveRange(db.LocationAttributeValues.Where(c => c.NodeId == rowId));
//         db.TableAttributeValues.RemoveRange(db.TableAttributeValues.Where(c => c.NodeId == rowId));
//     }
//
//     // ---------- 辅助：把值实体加到对应 DbSet ----------
//     private void AddValueEntity(AttributeValueBase entity)
//     {
//         switch (entity)
//         {
//             case TextAttributeValue t: db.TextAttributeValues.Add(t); break;
//             case DecimalAttributeValue dec: db.DecimalAttributeValues.Add(dec); break;
//             case IntegerAttributeValue i: db.IntegerAttributeValues.Add(i); break;
//             case DateAttributeValue da: db.DateAttributeValues.Add(da); break;
//             case TimeAttributeValue tm: db.TimeAttributeValues.Add(tm); break;
//             case DateTimeAttributeValue dtt: db.DateTimeAttributeValues.Add(dtt); break;
//             case FileAttributeValue f: db.FileAttributeValues.Add(f); break;
//             case LocationAttributeValue l: db.LocationAttributeValues.Add(l); break;
//             case TableAttributeDefValue tv: db.TableAttributeValues.Add(tv); break;
//             default: throw new InvalidOperationException($"未知值实体类型 {entity.GetType().Name}");
//         }
//     }
//
//     // ---------- 辅助：取列值原始值（用于序列化返回）----------
//     private static object? GetRawValue(AttributeValueBase v) => v switch
//     {
//         TextAttributeValue t => t.Value,
//         DecimalAttributeValue dec => dec.Value,
//         IntegerAttributeValue i => i.Value,
//         DateAttributeValue da => da.Value,
//         TimeAttributeValue tm => tm.Value,
//         DateTimeAttributeValue dtt => dtt.Value,
//         FileAttributeValue f => f.Value,
//         LocationAttributeValue l => new { l.Latitude, l.Longitude },
//         TableAttributeDefValue tv => tv.Value,
//         _ => null
//     };
// }