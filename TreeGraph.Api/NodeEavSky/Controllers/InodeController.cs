using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Infrastructure;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

/// <summary>
/// iNode 聚合端点。
///
/// 定位方式：
///   - 声明：/api/inode/{inodeId}/types/{entityType}
///   - 实体：/api/inode/{inodeId}/entities/{entityType}
///
/// URL 里没有 entityId —— 由服务端首次 PUT 时自动生成。
/// 转发到现有 EAV 管道（EavWriteService.SaveAsync(entityId, entityType, ...)）。
/// </summary>
[ApiController]
[Route("api/inode/{inodeId}")]
public class InodeController : ControllerBase
{
    private readonly IInodeEntityService _inodeEntities;
    private readonly InodeEavFacade _facade;
    private readonly IAttributeCache _attrCache;
    private readonly TreeGraphDbContext _db;
    private readonly CompositeValueService _compositeService;
    private readonly JsonSerializerOptions _jsonOptions;

    public InodeController(
        IInodeEntityService inodeEntities,
        InodeEavFacade facade,
        IAttributeCache attrCache,
        TreeGraphDbContext db,
        CompositeValueService compositeService,
        IOptions<JsonOptions> jsonOptions)
    {
        _inodeEntities = inodeEntities;
        _facade = facade;
        _attrCache = attrCache;
        _db = db;
        _compositeService = compositeService;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    // ============================================================
    // 声明层
    // ============================================================

    /// <summary>
    /// 列出该 iNode 下所有类型卡片（声明的 + 未声明的但全局可用的）。
    /// </summary>
    [HttpGet("types")]
    public async Task<IActionResult> ListTypes(
        string inodeId, CancellationToken ct)
    {
        var declarations = await _inodeEntities.ListDeclarationsAsync(inodeId, ct);
        var mappings = await _inodeEntities.ListByInodeAsync(inodeId, ct);
        var mappingByType = mappings.ToDictionary(m => m.EntityType);

        // 全局类型（直接查 entity_type_catalog）
        var globalTypes = await _db.EntityTypes
            .Where(t => !t.IsDeleted)
            .ToListAsync(ct);

        var cards = new List<InodeTypeCardDto>();

        foreach (var decl in declarations)
        {
            var mapping = mappingByType.GetValueOrDefault(decl.EntityType);
            var gt = globalTypes.FirstOrDefault(t => t.EntityType == decl.EntityType);

            cards.Add(new InodeTypeCardDto(
                decl.EntityType,
                gt?.DisplayName ?? decl.EntityType,
                gt?.EntityTypeId,
                gt?.Description,
                Declared: true,
                HasEntity: mapping is not null,
                EntityUpdatedAt: null));   // 需要时再填
        }

        return Ok(cards);
    }

    [HttpPost("types/{entityType}")]
    public async Task<IActionResult> Attach(
        string inodeId, string entityType, CancellationToken ct)
    {
        try
        {
            await _inodeEntities.AttachAsync(inodeId, entityType, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("types/{entityType}")]
    public async Task<IActionResult> Detach(
        string inodeId, string entityType, CancellationToken ct)
    {
        try
        {
            var ok = await _inodeEntities.DetachAsync(inodeId, entityType, ct);
            if (!ok) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ============================================================
    // 实体 CRUD
    // ============================================================

    /// <summary>
    /// 列出该 iNode 下所有已创建实体。
    /// </summary>
    [HttpGet("entities")]
    public async Task<IActionResult> ListEntities(
        string inodeId,
        [FromQuery] string? unit,
        CancellationToken ct)
    {
        var originalUnits = unit == "original";
        var all = await _facade.LoadAllByInodeAsync(inodeId, originalUnits, ct);

        var result = all.Values.Select(e => new DynamicEntityDto(
            e.EntityId,
            e.EntityType,
            ToJsonDict(e),
            e.UpdatedAt)).ToList();

        return Ok(result);
    }

    /// <summary>
    /// 读单个实体。未创建返回 404。
    /// </summary>
    [HttpGet("entities/{entityType}")]
    public async Task<IActionResult> GetEntity(
        string inodeId, string entityType,
        [FromQuery] string? unit,
        CancellationToken ct)
    {
        var originalUnits = unit == "original";
        var entity = await _facade.LoadAsync(
            inodeId, entityType, originalUnits, ct);
        if (entity is null) return NotFound();

        return Ok(new DynamicEntityDto(
            entity.EntityId,
            entity.EntityType,
            ToJsonDict(entity),
            entity.UpdatedAt));
    }

    /// <summary>
    /// PUT 全量替换。首次调用自动创建实体。
    /// </summary>
    [HttpPut("entities/{entityType}")]
    [IdempotentWrite]
    public async Task<IActionResult> Put(
        string inodeId, string entityType,
        [FromBody] Dictionary<string, JsonElement> values,
        CancellationToken ct)
    {
        var expected = ParseExpectedHeader();

        try
        {
            var typed = ConvertBody(entityType, values);
            await _facade.SaveAsync(inodeId, entityType, typed,
                User.Identity?.Name ?? "system",
                HttpContext.TraceIdentifier,
                ct, expected);
            return NoContent();
        }
        catch (EavConcurrencyException ex)
        {
            return Conflict(new
            {
                error = "并发冲突：实体已被其他用户修改，请刷新后重试",
                currentUpdatedAt = ex.CurrentUpdatedAt,
                expectedUpdatedAt = ex.ExpectedUpdatedAt
            });
        }
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// PATCH 部分更新。
    /// </summary>
    [HttpPatch("entities/{entityType}")]
    [IdempotentWrite]
    public async Task<IActionResult> Patch(
        string inodeId, string entityType,
        [FromBody] Dictionary<string, JsonElement> values,
        CancellationToken ct)
    {
        var expected = ParseExpectedHeader();

        try
        {
            var typed = ConvertBody(entityType, values);
            await _facade.PatchAsync(inodeId, entityType, typed,
                User.Identity?.Name ?? "system",
                HttpContext.TraceIdentifier,
                ct, expected);
            return NoContent();
        }
        catch (EavConcurrencyException ex)
        {
            return Conflict(new
            {
                error = "并发冲突：实体已被其他用户修改，请刷新后重试",
                currentUpdatedAt = ex.CurrentUpdatedAt,
                expectedUpdatedAt = ex.ExpectedUpdatedAt
            });
        }
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("entities/{entityType}")]
    [IdempotentWrite]
    public async Task<IActionResult> DeleteEntity(
        string inodeId, string entityType,
        CancellationToken ct)
    {
        var ok = await _facade.DeleteAsync(
            inodeId, entityType,
            User.Identity?.Name ?? "system",
            HttpContext.TraceIdentifier,
            ct);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// 审计历史。
    /// </summary>
    [HttpGet("entities/{entityType}/history")]
    public async Task<IActionResult> History(
        string inodeId, string entityType,
        [FromQuery] DateTimeOffset? from,
        CancellationToken ct)
    {
        var history = await _facade.GetHistoryAsync(
            inodeId, entityType, from, ct);

        var result = history.Select(a => new EntityHistoryDto(
            a.AuditId,
            a.EntityId,
            a.EntityType,
            a.AttributeId,
            a.AttributeName,
            a.OldValue,
            a.NewValue,
            a.ChangeType,
            a.ChangedBy,
            a.ChangedAt,
            a.CorrelationId,
            a.ClientIp)).ToList();

        return Ok(result);
    }

    // ============================================================
    // 辅助
    // ============================================================

    private DateTimeOffset? ParseExpectedHeader()
    {
        var headerValue = Request.Headers["X-Expected-Updated-At"]
            .FirstOrDefault();
        if (string.IsNullOrEmpty(headerValue)) return null;

        if (DateTimeOffset.TryParse(
                headerValue,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
            return parsed;

        return null;
    }

    private Dictionary<string, object?> ConvertBody(
        string entityType, Dictionary<string, JsonElement> values)
    {
        var defs = _attrCache.GetDefinitions(entityType)
            .ToDictionary(d => d.AttributeName);

        return EavRequestBodyConverter.Convert(values, defs, _compositeService);
    }

    private Dictionary<string, JsonElement> ToJsonDict(DynamicEntity e)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in e.Properties)
        {
            dict[k] = v is null
                ? JsonDocument.Parse("null").RootElement.Clone()
                : JsonSerializer.SerializeToElement(v, v.GetType(), _jsonOptions);
        }
        return dict;
    }

    // ============================================================
    // 全景 JSON
    // ============================================================

    /// <summary>
    /// 返回该 iNode 下所有实体的全部属性值（聚合 JSON）。
    ///
    /// 用途：外部应用一次性拉全景数据。
    ///
    /// 参数：
    ///   displayName  - false（默认）：属性 key 用 AttributeName（英文内部标识）
    ///                  true：属性 key 用 DisplayName（中文显示名）
    ///   includeNull  - false（默认）：跳过值为 null 的属性
    ///                  true：保留 null 键
    ///   units        - default（默认）：数值保留 { value, unitId } 对象
    ///                  base：数值归一化到基准单位，返回裸数字
    ///                  original：数值按原始输入单位还原，返回裸数字
    /// </summary>
    [HttpGet("json")]
    [Produces("application/json")]
    public async Task<IActionResult> GetAllAsJson(
        string inodeId,
        [FromQuery] bool displayName = false,
        [FromQuery] bool includeNull = false,
        [FromQuery] string? units = null,
        CancellationToken ct = default)
    {
        // ---- 1. 参数校验 ----
        var unitsMode = units?.ToLowerInvariant();
        if (unitsMode is not (null or "default" or "base" or "original"))
        {
            return BadRequest(new
            {
                error = "units 参数必须是 default / base / original 之一"
            });
        }

        // base 和 original 都返回裸数字，但取值方式不同：
        //   - base:     归一化值（后端默认存储）
        //   - original: 还原到用户原始输入单位
        var originalUnits = unitsMode == "original";
        var bareValue = unitsMode is "base" or "original";

        // ---- 2. 加载所有实体（复用现有 Facade） ----
        var all = await _facade.LoadAllByInodeAsync(inodeId, originalUnits, ct);

        // ---- 3. 构建 entities 字典 ----
        var entitiesDict = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (entityType, entity) in all)
        {
            // 属性定义（用于 displayName 映射）
            var defs = _attrCache.GetDefinitions(entityType)
                .ToDictionary(d => d.AttributeName, StringComparer.Ordinal);

            var props = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var (attrName, rawValue) in entity.Properties)
            {
                // 空值处理
                if (rawValue is null && !includeNull) continue;

                // key 选择
                var key = attrName;
                if (displayName
                    && defs.TryGetValue(attrName, out var def)
                    && !string.IsNullOrWhiteSpace(def.DisplayName))
                {
                    key = def.DisplayName;
                }

                // 值转换
                props[key] = ConvertPropertyValue(rawValue, bareValue);
            }

            // includeNull=true：该类型已定义但实体中未写入的属性补 null 键。
            // （未写入的属性没有 attribute_value 行，entity.Properties 里键不存在，
            //   仅靠上面 rawValue is null 分支无法补齐。）
            if (includeNull)
            {
                foreach (var def in _attrCache.GetDefinitions(entityType))
                {
                    var defKey = (displayName
                            && !string.IsNullOrWhiteSpace(def.DisplayName))
                        ? def.DisplayName
                        : def.AttributeName;
                    if (!props.ContainsKey(defKey)) props[defKey] = null;
                }
            }

            entitiesDict[entityType] = new
            {
                entityId = entity.EntityId,
                updatedAt = entity.UpdatedAt,
                properties = props
            };
        }

        // ---- 4. 序列化 ----
        // 注意：DynamicCompositeValue 已在 Program.cs 里注册了自定义转换器，
        //      JsonSerializer.Serialize 会自动应用。
        var result = new
        {
            inodeId,
            generatedAt = DateTimeOffset.UtcNow,
            entities = entitiesDict
        };

        var json = JsonSerializer.Serialize(result, _jsonOptions);

        // 直接返回 JSON 字符串（Content-Type: application/json）
        return Content(json, "application/json");
    }

    /// <summary>
    /// 属性值 → JSON 友好形式。
    ///
    ///   bareValue = false：数值保留 { value, unitId } 对象（含原始输入单位）
    ///   bareValue = true：数值只返回裸 decimal（用于 base / original 模式）
    /// </summary>
    private static object? ConvertPropertyValue(object? value, bool bareValue)
    {
        if (value is null) return null;

        return value switch
        {
            // 数值 + 单位
            NumericValue nv => bareValue
                ? (object)nv.Value
                : new { value = nv.Value, unitId = nv.UnitId },

            // 单选值
            SingleChoiceValue sc => new { value = sc.Value, label = sc.Label },

            // 其它类型：DateOnly / TimeOnly / DateTimeOffset / bool / string /
            //           long / decimal / JsonDocument / DynamicCompositeValue
            //           → 由 JsonSerializer 自动处理
            _ => value
        };
    }
}
