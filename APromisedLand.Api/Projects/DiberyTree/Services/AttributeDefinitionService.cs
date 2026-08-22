using APromisedLand.Api.Data;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Validation;
using Microsoft.EntityFrameworkCore;

namespace APromisedLand.Api.Projects.DiberyTree.Services;

/// <summary>
/// 属性定义（含动态表定义与列定义）的业务服务。
/// 签名对齐 Controller 风格：返回 DTO + 抛异常（ArgumentException/KeyNotFoundException/InvalidOperationException）。
/// 创建/更新时调用 <see cref="DefinitionValidator"/> 拒绝非法定义（如列不可为「表」类型）。
/// </summary>
public class AttributeDefinitionService(DiberyDbContext db)
{
    // ---------- 查询 ----------

    /// <summary>获取单个属性定义（含类型/单位/父表）。</summary>
    public async Task<AttributeDefinitionDto?> GetByIdAsync(string id)
    {
        var def = await db.AttributeDefinitions
            // 移除了 .Include(d => d.AttributeType)
            .Include(d => d.Unit)
            .Include(d => d.Parent)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
        return def?.ToDto();
    }

    /// <summary>获取所有属性定义。</summary>
    public async Task<IReadOnlyList<AttributeDefinitionDto>> GetAllAsync()
    {
        var list = await db.AttributeDefinitions
            .Where(x  => x.ParentId == null)
            // 移除了 .Include(d => d.AttributeType)
            .Include(d => d.Unit)
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync();
        return list.Select(d => d.ToDto()).ToList();
    }

    /// <summary>列出所有「表定义」（ParentId 为空且类型=表）。</summary>
    public async Task<List<AttributeDefinitionDto>> ListTablesAsync()
    {
        // 改用 AttributeTypeMapping 获取表格类型的 ID
        var tableTypeId = AttributeTypeEnum.表格.ToAttributeTypeId();
        var tables = await db.AttributeDefinitions
            // 移除了 .Include(d => d.AttributeType)
            .Include(d => d.Unit)
            .AsNoTracking()
            .Where(d => d.ParentId == null && d.AttributeTypeId == tableTypeId)
            .OrderBy(d => d.Name)
            .ToListAsync();
        
        return tables.Select(d => d.ToDto()).ToList();
    }

    /// <summary>列出指定表下的所有列定义，按 Order 排序。</summary>
    public async Task<List<AttributeDefinitionDto>> ListColumnsAsync(string tableId)
    {
        var cols = await db.AttributeDefinitions
            // 移除了 .Include(d => d.AttributeType)
            .Include(d => d.Unit)
            .AsNoTracking()
            .Where(d => d.ParentId == tableId)
            .OrderBy(d => d.Order)
            .ToListAsync();
        return cols.Select(d => d.ToDto()).ToList();
    }

    // ---------- 创建 ----------

    /// <summary>创建属性定义（表定义或列定义）。非合法时抛 ArgumentException。</summary>
    public async Task<AttributeDefinitionDto> CreateAsync(AttributeDefinitionCreateDto dto)
    {
        // 假设 dto.AttributeType 现在是 AttributeTypeEnum 类型
        var def = new AttributeDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = dto.Name,
            AttributeTypeId = dto.AttributeType.ToAttributeTypeId(), // 转换为 ID
            // 删除了 AttributeType = dto.AttributeType（导航已移除）
            Lines = dto.Lines,
            MaxLength = dto.MaxLength ?? 30,
            Precision = dto.Precision,
            Scale = dto.Scale,
            UnitId = dto.UnitId,
            ParentId = dto.ParentId,
            Order = dto.Order,
            IsRequired = dto.IsRequired,
            DefaultValue = dto.DefaultValue,
        };

        // 结构性校验：表定义必须为表类型；列定义不可为表类型
        var (ok, err) = DefinitionValidator.Validate(def);
        if (!ok) throw new ArgumentException(err);

        // 列定义：校验父表存在
        if (!string.IsNullOrEmpty(def.ParentId))
        {
            var parentExists = await db.AttributeDefinitions.AnyAsync(d => d.Id == def.ParentId);
            if (!parentExists) throw new ArgumentException($"所属表定义 '{def.ParentId}' 不存在");
        }

        db.AttributeDefinitions.Add(def);
        await db.SaveChangesAsync();

        // 重新查询返回完整 DTO（含导航）
        var created = await db.AttributeDefinitions
            // 移除了 .Include(d => d.AttributeType)
            .Include(d => d.Unit)
            .Include(d => d.Parent)
            .AsNoTracking()
            .FirstAsync(d => d.Id == def.Id);
        return created.ToDto();
    }

    /// <summary>
    /// 在指定表下新建列定义：强制 ParentId=tableId，校验列类型不为「表格」。
    /// 对前端更友好——body 无需传 ParentId。
    /// </summary>
    /// <exception cref="KeyNotFoundException">tableId 不存在或不是表定义</exception>
    /// <exception cref="ArgumentException">列定义非法（如类型=表格）</exception>
    public async Task<AttributeDefinitionDto> CreateTableColumnAsync(
        string tableId, AttributeDefinitionCreateDto dto)
    {
        // 获取表定义并检查是否为表格类型
        var table = await db.AttributeDefinitions
            // 不再需要 Include，直接通过 AttributeTypeId 判断
            .FirstOrDefaultAsync(d => d.Id == tableId)
            ?? throw new KeyNotFoundException($"表定义 '{tableId}' 不存在");

        // 检查是否为表定义：ParentId 为空且类型为表格
        if (table.ParentId != null)
            throw new KeyNotFoundException($"'{tableId}' 不是表定义（有父级）");

        if (!AttributeTypeMapping.IdToEnum.TryGetValue(table.AttributeTypeId, out var tableType) || tableType != AttributeTypeEnum.表格)
            throw new KeyNotFoundException($"'{tableId}' 不是表定义（类型不匹配）");

        dto.ParentId = tableId;
        return await CreateAsync(dto);
    }

    // ---------- 更新 ----------

    /// <summary>更新属性定义（类型不可改）。不存在抛 KeyNotFoundException，非合法抛 ArgumentException。</summary>
    public async Task<AttributeDefinitionDto> UpdateAsync(string id, AttributeDefinitionUpdateDto dto)
    {
        var def = await db.AttributeDefinitions
            // 移除了 .Include(d => d.AttributeType)
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException($"定义 '{id}' 不存在");

        if (!string.IsNullOrEmpty(dto.Name)) def.Name = dto.Name;
        if (dto.Lines.HasValue) def.Lines = dto.Lines;
        if (dto.MaxLength.HasValue) def.MaxLength = dto.MaxLength.Value;
        if (dto.Precision.HasValue) def.Precision = dto.Precision;
        if (dto.Scale.HasValue) def.Scale = dto.Scale;
        if (dto.UnitId is not null) def.UnitId = dto.UnitId;
        if (dto.Order.HasValue) def.Order = dto.Order.Value;
        if (dto.IsRequired.HasValue) def.IsRequired = dto.IsRequired.Value;
        if (dto.DefaultValue is not null) def.DefaultValue = dto.DefaultValue;

        var (ok, err) = DefinitionValidator.Validate(def);
        if (!ok) throw new ArgumentException(err);

        db.Update(def);
        await db.SaveChangesAsync();

        var updated = await db.AttributeDefinitions
            // 移除了 .Include(d => d.AttributeType)
            .Include(d => d.Unit)
            .Include(d => d.Parent)
            .AsNoTracking()
            .FirstAsync(d => d.Id == id);
        return updated.ToDto();
    }

    // ---------- 删除 ----------

    /// <summary>删除属性定义。表定义级联删其下列定义。被值引用时抛 InvalidOperationException。</summary>
    public async Task<bool> DeleteAsync(string id)
    {
        var def = await db.AttributeDefinitions.FindAsync(id)
            ?? throw new KeyNotFoundException($"定义 '{id}' 不存在");

        // 被属性值引用则禁止删除
        if (await IsReferencedByValuesAsync(id))
            throw new InvalidOperationException($"定义 '{def.Name}' 正被属性值引用，无法删除");

        // 表定义：先删其下所有列定义
        if (string.IsNullOrEmpty(def.ParentId))
        {
            var children = await db.AttributeDefinitions
                .Where(d => d.ParentId == id)
                .ToListAsync();
            db.AttributeDefinitions.RemoveRange(children);
        }

        db.AttributeDefinitions.Remove(def);
        await db.SaveChangesAsync();
        return true;
    }

    // ---------- 辅助：检查定义是否被任一 typed value 引用 ----------
    private async Task<bool> IsReferencedByValuesAsync(string defId)
    {
        if (await db.TextAttributeValues.AnyAsync(v => v.AttributeDefinitionId == defId)) return true;
        if (await db.DecimalAttributeValues.AnyAsync(v => v.AttributeDefinitionId == defId)) return true;
        if (await db.IntegerAttributeValues.AnyAsync(v => v.AttributeDefinitionId == defId)) return true;
        if (await db.DateAttributeValues.AnyAsync(v => v.AttributeDefinitionId == defId)) return true;
        if (await db.TimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == defId)) return true;
        if (await db.DateTimeAttributeValues.AnyAsync(v => v.AttributeDefinitionId == defId)) return true;
        if (await db.FileAttributeValues.AnyAsync(v => v.AttributeDefinitionId == defId)) return true;
        if (await db.LocationAttributeValues.AnyAsync(v => v.AttributeDefinitionId == defId)) return true;
        if (await db.TableRowAttributeValues.AnyAsync(v => v.AttributeDefinitionId == defId)) return true;
        return false;
    }
}