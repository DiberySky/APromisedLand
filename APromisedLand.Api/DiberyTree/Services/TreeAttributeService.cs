// using System.Text.Json;
// using APromisedLand.Api.Data;
// using APromisedLand.Api.DiberyTree.Interface;
// using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
// using APromisedLand.Shared.DiberyTree.Attributes.Enums;
// using APromisedLand.Shared.DiberyTree.Attributes.Models;
// using APromisedLand.Shared.DiberyTree.Attributes.Validation;
// using APromisedLand.Shared.DiberyTree.Models;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace APromisedLand.Api.DiberyTree.Services;
//
// public class TreeAttributeService(DiberyDbContext dbContext, ILogger<TreeAttributeService> logger)
//     : ITreeAttributeService
// {
//     // ---------- 属性定义 ----------
//     public async Task<IReadOnlyList<AttributeDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
//     {
//         var query = from def in dbContext.AttributeDefinitions
//             where def.ParentId == null
//             join unit in dbContext.UnitTrees on def.UnitId equals unit.Id into units
//             from u in units.DefaultIfEmpty()
//             select new AttributeDefinitionDto
//             {
//                 Id = def.Id,
//                 Name = def.Name,
//                 AttributeType = def.AttributeTypeId.ToAttributeTypeEnum(),
//                 MaxLength = def.MaxLength,
//                 Lines = def.Lines,
//                 Precision = def.Precision,
//                 Scale = def.Scale,
//                 Unit = u
//             };
//         return await query.ToListAsync(cancellationToken);
//     }
//
//     public async Task<AttributeDefinitionDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
//     {
//         var query = from def in dbContext.AttributeDefinitions
//             where def.Id == id
//             join unit in dbContext.UnitTrees on def.UnitId equals unit.Id into units
//             from u in units.DefaultIfEmpty()
//             select new AttributeDefinitionDto
//             {
//                 Id = def.Id,
//                 Name = def.Name,
//                 AttributeType = def.AttributeTypeId.ToAttributeTypeEnum(),
//                 MaxLength = def.MaxLength,
//                 Lines = def.Lines,
//                 Precision = def.Precision,
//                 Scale = def.Scale,
//                 Unit = u
//             };
//         return await query.FirstOrDefaultAsync(cancellationToken);
//     }
//
//     public async Task<AttributeDefinitionDto> CreateAsync(AttributeDefinitionCreateDto dto,
//         CancellationToken cancellationToken = default)
//     {
//         // 验证枚举有效性
//         if (!Enum.IsDefined(typeof(AttributeTypeEnum), dto.AttributeType))
//             throw new ArgumentException("无效的属性类型。");
//
//         // 检查单位是否存在（如果提供）
//         if (!string.IsNullOrEmpty(dto.UnitId))
//         {
//             var unitExists = await dbContext.UnitTrees.AnyAsync(u => u.Id == dto.UnitId, cancellationToken);
//             if (!unitExists)
//                 throw new ArgumentException($"单位 '{dto.UnitId}' 不存在。");
//         }
//
//         var entity = new AttributeDefinition
//         {
//             Id = Guid.NewGuid().ToString(),
//             Name = dto.Name,
//             AttributeTypeId = dto.AttributeType.ToAttributeTypeId(), // 扩展方法
//             MaxLength = dto.MaxLength ?? 30,
//             Lines = dto.Lines ?? 1,
//             Precision = dto.Precision,
//             Scale = dto.Scale,
//             UnitId = dto.UnitId
//         };
//
//         await dbContext.AttributeDefinitions.AddAsync(entity, cancellationToken);
//         await dbContext.SaveChangesAsync(cancellationToken);
//
//         return await GetByIdAsync(entity.Id, cancellationToken)
//                ?? throw new Exception("创建后无法检索实体。");
//     }
//
//     public async Task<AttributeDefinitionDto> UpdateAsync(string id, AttributeDefinitionUpdateDto dto,
//         CancellationToken cancellationToken = default)
//     {
//         var entity = await dbContext.AttributeDefinitions.FindAsync(new object[] { id }, cancellationToken);
//         if (entity == null) throw new KeyNotFoundException($"未找到 ID 为 '{id}' 的属性定义。");
//
//         entity.Name = dto.Name;
//         entity.Lines = dto.Lines;
//         entity.Precision = dto.Precision;
//         entity.Scale = dto.Scale;
//         entity.UnitId = dto.UnitId;
//         // 若允许修改类型，可在此添加 entity.AttributeTypeId = dto.AttributeType.ToAttributeTypeId();
//
//         await dbContext.SaveChangesAsync(cancellationToken);
//         return await GetByIdAsync(id, cancellationToken) ?? throw new Exception("更新后无法检索实体。");
//     }
//
//     public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
//     {
//         var entity = await dbContext.AttributeDefinitions.FindAsync(new object[] { id }, cancellationToken);
//         if (entity == null) return false;
//
//         bool hasReferences =
//             await dbContext.TextAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
//             || await dbContext.IntegerAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
//             || await dbContext.DecimalAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
//             || await dbContext.DateAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
//             || await dbContext.TimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
//             || await dbContext.DateTimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
//             || await dbContext.FileAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
//             || await dbContext.LocationAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken)
//             || await dbContext.TableAttributeValues.AnyAsync(v => v.AttributeDefinitionId == id, cancellationToken);
//
//         if (hasReferences)
//             throw new InvalidOperationException("无法删除定义，因为它正被一个或多个节点属性值使用。");
//
//         dbContext.AttributeDefinitions.Remove(entity);
//         await dbContext.SaveChangesAsync(cancellationToken);
//         return true;
//     }
//
//     // ---------- 属性值操作 ----------
//     public async Task<AttributeValueBase> AddValueAsync(string nodeId, AddValueDto dto)
//     {
//         // 1. 获取定义
//         var definition = await dbContext.AttributeDefinitions
//             .FirstOrDefaultAsync(d => d.Id == dto.AttributeDefinitionId);
//         if (definition == null)
//             throw new KeyNotFoundException($"属性定义 '{dto.AttributeDefinitionId}' 不存在。");
//
//         // 2. 从映射获取枚举类型
//         if (!AttributeTypeMapping.IdToEnum.TryGetValue(definition.AttributeTypeId, out var attrType))
//             throw new InvalidOperationException($"无法识别属性类型ID: {definition.AttributeTypeId}");
//
//         // 3. 验证并构建值实体（传入枚举类型）
//         var validation = ValueValidator.ValidateAndBuild(definition, dto.Value, nodeId);
//         if (!validation.IsValid)
//             throw new ArgumentException(validation.ErrorMessage);
//
//         var entity = validation.ValueEntity!;
//         entity.Id = Guid.NewGuid().ToString();
//
//         // 4. 处理日期时间类型（UTC转换）
//         if (entity is DateTimeAttributeValue dt)
//         {
//             var original = dt.Value;
//             dt.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
//             logger.LogInformation(
//                 "DateTimeAttributeValue 时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
//                 original, original.Offset, dt.Value);
//         }
//         else if (entity is DateAttributeValue d)
//         {
//             var original = d.Value;
//             d.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
//             logger.LogInformation(
//                 "DateAttributeValue 时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
//                 original, original.Offset, d.Value);
//         }
//
//         // 5. 保存
//         switch (entity)
//         {
//             case TextAttributeValue tv: await dbContext.TextAttributeValues.AddAsync(tv); break;
//             case DecimalAttributeValue dv: await dbContext.DecimalAttributeValues.AddAsync(dv); break;
//             case IntegerAttributeValue iv: await dbContext.IntegerAttributeValues.AddAsync(iv); break;
//             case DateAttributeValue dav: await dbContext.DateAttributeValues.AddAsync(dav); break;
//             case TimeAttributeValue tav: await dbContext.TimeAttributeValues.AddAsync(tav); break;
//             case DateTimeAttributeValue dtav: await dbContext.DateTimeAttributeValues.AddAsync(dtav); break;
//             case FileAttributeValue fav: await dbContext.FileAttributeValues.AddAsync(fav); break;
//             case LocationAttributeValue lav: await dbContext.LocationAttributeValues.AddAsync(lav); break;
//             case TableAttributeDefValue tv: await dbContext.TableAttributeValues.AddAsync(tv); break;
//             default: throw new NotSupportedException($"不支持的值类型 '{entity.GetType().Name}'。");
//         }
//
//         await dbContext.SaveChangesAsync();
//         return entity;
//     }
//
//     public async Task<AttributeDto?> GetValueAsync(string nodeId, string id)
//     {
//         var value = await FindValueAsync(nodeId, id);
//         if (value == null) return null;
//
//         var info = await GetDefinitionWithTypeAndUnitAsync(value.AttributeDefinitionId);
//         if (info == null) return null;
//
//         return MapToDto(value, info.Definition, info.AttributeType, info.Unit);
//     }
//
//     public async Task<NodeDto> GetAllValuesAsync(string nodeId)
//     {
//         var list = new List<AttributeDto>();
//         list.AddRange(await QueryValues<TextAttributeValue>(nodeId));
//         list.AddRange(await QueryValues<DecimalAttributeValue>(nodeId));
//         list.AddRange(await QueryValues<IntegerAttributeValue>(nodeId));
//         list.AddRange(await QueryValues<DateAttributeValue>(nodeId));
//         list.AddRange(await QueryValues<TimeAttributeValue>(nodeId));
//         list.AddRange(await QueryValues<DateTimeAttributeValue>(nodeId));
//         list.AddRange(await QueryValues<FileAttributeValue>(nodeId));
//         list.AddRange(await QueryValues<LocationAttributeValue>(nodeId));
//         list.AddRange(await QueryValues<TableAttributeDefValue>(nodeId));
//         return new NodeDto { Id = nodeId, AttributeDtos = list };
//     }
//
//     public async Task<bool> DeleteValueAsync(string nodeId, string id)
//     {
//         var value = await FindValueAsync(nodeId, id);
//         if (value == null) return false;
//         dbContext.Remove(value);
//         await dbContext.SaveChangesAsync();
//         return true;
//     }
//
//     public async Task UpdateValueAsync(string nodeId, string id, JsonElement value, CancellationToken cancellationToken)
//     {
//         // 1. 查找现有值
//         var existing = await FindValueAsync(nodeId, id);
//         if (existing == null)
//             throw new KeyNotFoundException($"属性值 '{id}' 在节点 '{nodeId}' 中不存在。");
//
//         // 2. 获取定义和类型枚举
//         var def = await dbContext.AttributeDefinitions
//             .FirstOrDefaultAsync(d => d.Id == existing.AttributeDefinitionId, cancellationToken);
//         if (def == null)
//             throw new KeyNotFoundException($"属性定义 '{existing.AttributeDefinitionId}' 不存在。");
//
//         if (!AttributeTypeMapping.IdToEnum.TryGetValue(def.AttributeTypeId, out var attrType))
//             throw new InvalidOperationException($"无法识别属性类型ID: {def.AttributeTypeId}");
//
//         // 3. 验证新值
//         var validation = ValueValidator.ValidateAndBuild(def, value, nodeId);
//         if (!validation.IsValid)
//             throw new ArgumentException(validation.ErrorMessage);
//
//         var newEntity = validation.ValueEntity!;
//
//         // 4. 复制值到现有实体
//         CopyValue(existing, newEntity);
//
//         // 5. 处理日期时间类型（UTC转换）
//         if (existing is DateTimeAttributeValue dt)
//         {
//             var original = dt.Value;
//             dt.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
//             logger.LogInformation(
//                 "DateTimeAttributeValue 更新时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
//                 original, original.Offset, dt.Value);
//         }
//         else if (existing is DateAttributeValue d)
//         {
//             var original = d.Value;
//             d.Value = new DateTimeOffset(original.UtcDateTime, TimeSpan.Zero);
//             logger.LogInformation(
//                 "DateAttributeValue 更新时区转换: 原始 {Original} (Offset={Offset}) -> 转换后 {Converted} (Offset=00:00)",
//                 original, original.Offset, d.Value);
//         }
//
//         // 6. 保存
//         await dbContext.SaveChangesAsync(cancellationToken);
//     }
//
//     // ---------- 私有辅助方法 ----------
//     private async Task<AttributeValueBase?> FindValueAsync(string nodeId, string id)
//     {
//         var tasks = new Func<Task<AttributeValueBase?>>[]
//         {
//             async () => await dbContext.TextAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
//             async () =>
//                 await dbContext.DecimalAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
//             async () =>
//                 await dbContext.IntegerAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
//             async () => await dbContext.DateAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
//             async () => await dbContext.TimeAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
//             async () => await dbContext.DateTimeAttributeValues.FirstOrDefaultAsync(v =>
//                 v.NodeId == nodeId && v.Id == id),
//             async () => await dbContext.FileAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
//             async () => await dbContext.LocationAttributeValues.FirstOrDefaultAsync(v =>
//                 v.NodeId == nodeId && v.Id == id),
//             async () => await dbContext.TableAttributeValues.FirstOrDefaultAsync(v => v.NodeId == nodeId && v.Id == id),
//         };
//         foreach (var task in tasks)
//         {
//             var result = await task();
//             if (result != null) return result;
//         }
//
//         return null;
//     }
//
//     private async Task<DefinitionWithTypeAndUnit?> GetDefinitionWithTypeAndUnitAsync(string definitionId)
//     {
//         var def = await dbContext.AttributeDefinitions
//             .FirstOrDefaultAsync(d => d.Id == definitionId);
//         if (def == null) return null;
//
//         if (!AttributeTypeMapping.IdToEnum.TryGetValue(def.AttributeTypeId, out var attrType))
//             return null;
//
//         UnitTree? unit = null;
//         if (!string.IsNullOrEmpty(def.UnitId))
//             unit = await dbContext.UnitTrees.FindAsync(def.UnitId);
//
//         return new DefinitionWithTypeAndUnit
//         {
//             Definition = def,
//             AttributeType = attrType,
//             Unit = unit
//         };
//     }
//
//     private async Task<List<AttributeDto>> QueryValues<TValue>(string nodeId) where TValue : AttributeValueBase
//     {
//         var items = await dbContext.Set<TValue>().Where(v => v.NodeId == nodeId).ToListAsync();
//         if (items.Count == 0) return new List<AttributeDto>();
//
//         var result = new List<AttributeDto>();
//         foreach (var v in items)
//         {
//             var info = await GetDefinitionWithTypeAndUnitAsync(v.AttributeDefinitionId);
//             if (info != null)
//                 result.Add(MapToDto(v, info.Definition, info.AttributeType, info.Unit));
//         }
//
//         return result;
//     }
//
//     private static AttributeDto MapToDto(AttributeValueBase v, AttributeDefinition? def, AttributeTypeEnum attrType,
//         UnitTree? unit)
//     {
//         // 如果 def 不为空，可设置一个未映射的属性（若实体中有）
//         // 但此处不需要，因为 DTO 已包含枚举
//
//         object? rawValue = v switch
//         {
//             TextAttributeValue tv => tv.Value,
//             DecimalAttributeValue dv => dv.Value,
//             IntegerAttributeValue iv => iv.Value,
//             DateAttributeValue dateV => dateV.Value,
//             TimeAttributeValue timeV => timeV.Value,
//             DateTimeAttributeValue dtV => dtV.Value,
//             FileAttributeValue fv => fv.Value,
//             LocationAttributeValue lv => new { lv.Latitude, lv.Longitude },
//             TableAttributeDefValue tv => tv.Value,
//             _ => null
//         };
//
//         var jsonElement = JsonSerializer.SerializeToElement(rawValue, new JsonSerializerOptions
//         {
//             Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
//         });
//
//         return new AttributeDto
//         {
//             Id = v.Id,
//             DefinitionId = v.AttributeDefinitionId,
//             Definition = def ?? new AttributeDefinition(),
//             Value = jsonElement
//         };
//     }
//
//     private static void CopyValue(AttributeValueBase target, AttributeValueBase source)
//     {
//         if (target.GetType() != source.GetType())
//             throw new InvalidOperationException("目标类型与源类型不匹配。");
//
//         switch (target)
//         {
//             case TextAttributeValue tTarget:
//                 var tSource = (TextAttributeValue)source;
//                 tTarget.Value = tSource.Value;
//                 break;
//             case DecimalAttributeValue dTarget:
//                 var dSource = (DecimalAttributeValue)source;
//                 dTarget.Value = dSource.Value;
//                 break;
//             case IntegerAttributeValue iTarget:
//                 var iSource = (IntegerAttributeValue)source;
//                 iTarget.Value = iSource.Value;
//                 break;
//             case DateAttributeValue dateTarget:
//                 var dateSource = (DateAttributeValue)source;
//                 dateTarget.Value = dateSource.Value;
//                 break;
//             case TimeAttributeValue timeTarget:
//                 var timeSource = (TimeAttributeValue)source;
//                 timeTarget.Value = timeSource.Value;
//                 break;
//             case DateTimeAttributeValue dtTarget:
//                 var dtSource = (DateTimeAttributeValue)source;
//                 dtTarget.Value = dtSource.Value;
//                 break;
//             case FileAttributeValue fTarget:
//                 var fSource = (FileAttributeValue)source;
//                 fTarget.Value = fSource.Value;
//                 break;
//             case LocationAttributeValue lTarget:
//                 var lSource = (LocationAttributeValue)source;
//                 lTarget.Latitude = lSource.Latitude;
//                 lTarget.Longitude = lSource.Longitude;
//                 break;
//             case TableAttributeDefValue tvTarget:
//                 var tvSource = (TableAttributeDefValue)source;
//                 tvTarget.Value = tvSource.Value;
//                 break;
//             default:
//                 throw new NotSupportedException($"不支持的值类型 '{target.GetType().Name}'。");
//         }
//     }
//
//     private sealed class DefinitionWithTypeAndUnit
//     {
//         public AttributeDefinition Definition { get; set; } = null!;
//         public AttributeTypeEnum AttributeType { get; set; }
//         public UnitTree? Unit { get; set; }
//     }
// }