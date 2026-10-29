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
///   - 绑定：/api/inode/{inodeId}/bindings/{bindingId}
///
/// 命名绑定：每条绑定（binding_id + name）拥有自己的一条私有 EAV 数据，
/// 因此同一 iNode 同一类型可以建立多条互不干扰的绑定。
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
        var bindings = await _inodeEntities.ListBindingsAsync(inodeId, ct);
        var bindingsByType = bindings
            .GroupBy(m => m.EntityType)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.AttachedAt).ToList());

        // 全局类型（直接查 entity_type_catalog）
        var globalTypes = await _db.EntityTypes
            .Where(t => !t.IsDeleted)
            .ToListAsync(ct);

        var cards = new List<InodeTypeCardDto>();

        foreach (var decl in declarations)
        {
            var typeBindings = bindingsByType.GetValueOrDefault(decl.EntityType);
            var bindingCount = typeBindings?.Count ?? 0;
            var gt = globalTypes.FirstOrDefault(t => t.EntityType == decl.EntityType);

            // 实体最近更新时间（取该类型下所有绑定实体的最大值）
            DateTimeOffset? entityUpdatedAt = null;
            if (typeBindings is not null)
            {
                foreach (var binding in typeBindings)
                {
                    var entity = await _facade.LoadAsync(
                        inodeId, binding.BindingId, ct);
                    if (entity?.UpdatedAt is { } updated
                        && (entityUpdatedAt is null || updated > entityUpdatedAt))
                    {
                        entityUpdatedAt = updated;
                    }
                }
            }

            cards.Add(new InodeTypeCardDto(
                decl.EntityType,
                HostTypeCatalog.DisplayNameOrSelf(decl.EntityType, gt?.DisplayName),
                gt?.EntityTypeId,
                gt?.Description,
                Declared: true,
                HasEntity: bindingCount > 0,
                EntityUpdatedAt: entityUpdatedAt,
                BindingCount: bindingCount));
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
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
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
    // 命名绑定 CRUD
    // ============================================================

    /// <summary>列出该 iNode 下所有命名绑定（含最近更新时间）。</summary>
    [HttpGet("bindings")]
    public async Task<IActionResult> ListBindings(
        string inodeId, CancellationToken ct)
    {
        var bindings = await _inodeEntities.ListBindingsAsync(inodeId, ct);
        if (bindings.Count == 0) return Ok(Array.Empty<InodeBindingDto>());

        var result = new List<InodeBindingDto>(bindings.Count);
        foreach (var b in bindings)
        {
            var entity = await _facade.LoadAsync(inodeId, b.BindingId, ct);
            result.Add(new InodeBindingDto(
                b.BindingId, b.Name, b.EntityType, b.EntityId,
                b.DisplayOrder, entity?.UpdatedAt));
        }
        return Ok(result);
    }

    /// <summary>
    /// 新建命名绑定（同名同类型重复会被拒绝）。
    /// 刻意不加 [IdempotentWrite]：创建本身不幂等。
    /// </summary>
    [HttpPost("bindings")]
    public async Task<IActionResult> CreateBinding(
        string inodeId,
        [FromBody] CreateBindingRequest request,
        CancellationToken ct)
    {
        try
        {
            var (bindingId, entityId) = await _facade.CreateBindingAsync(
                inodeId, request.EntityType, request.Name, ct);

            return Created(
                $"/api/inode/{inodeId}/bindings/{bindingId}",
                new { bindingId, entityId });
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

    /// <summary>读单条绑定（含属性值）。</summary>
    [HttpGet("bindings/{bindingId}")]
    public async Task<IActionResult> GetBinding(
        string inodeId, string bindingId,
        CancellationToken ct)
    {
        var binding = await _inodeEntities.GetBindingAsync(inodeId, bindingId, ct);
        if (binding is null) return NotFound();

        var entity = await _facade.LoadAsync(inodeId, bindingId, ct);

        return Ok(new InodeBindingDetailDto(
            binding.BindingId,
            binding.Name,
            binding.EntityType,
            binding.EntityId,
            entity is null ? new Dictionary<string, JsonElement>() : ToJsonDict(entity),
            entity?.UpdatedAt));
    }

    /// <summary>PUT 全量替换绑定数据。</summary>
    [HttpPut("bindings/{bindingId}")]
    [IdempotentWrite]
    public Task<IActionResult> Put(
        string inodeId, string bindingId,
        [FromBody] Dictionary<string, JsonElement> values,
        CancellationToken ct)
        => WriteAsync(inodeId, bindingId, values, replaceAll: true, ct);

    /// <summary>PATCH 部分更新绑定数据。</summary>
    [HttpPatch("bindings/{bindingId}")]
    [IdempotentWrite]
    public Task<IActionResult> Patch(
        string inodeId, string bindingId,
        [FromBody] Dictionary<string, JsonElement> values,
        CancellationToken ct)
        => WriteAsync(inodeId, bindingId, values, replaceAll: false, ct);

    /// <summary>重命名绑定（名字在该 (inode, type) 下唯一）。</summary>
    [HttpPatch("bindings/{bindingId}/name")]
    [IdempotentWrite]
    public async Task<IActionResult> RenameBinding(
        string inodeId, string bindingId,
        [FromBody] RenameBindingRequest request,
        CancellationToken ct)
    {
        try
        {
            var ok = await _facade.RenameBindingAsync(
                inodeId, bindingId, request.Name, ct);
            if (!ok) return NotFound();
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

    /// <summary>更新绑定显示顺序（相邻行互换时逐条提交）。</summary>
    [HttpPatch("bindings/{bindingId}/order")]
    [IdempotentWrite]
    public async Task<IActionResult> ReorderBinding(
        string inodeId, string bindingId,
        [FromBody] ReorderBindingRequest request,
        CancellationToken ct)
    {
        var ok = await _facade.ReorderBindingAsync(
            inodeId, bindingId, request.DisplayOrder, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>删除绑定：连同该绑定私有的属性数据一并删除。</summary>
    [HttpDelete("bindings/{bindingId}")]
    [IdempotentWrite]
    public async Task<IActionResult> DeleteBinding(
        string inodeId, string bindingId,
        CancellationToken ct)
    {
        var ok = await _facade.DeleteAsync(
            inodeId, bindingId,
            User.Identity?.Name ?? "system",
            HttpContext.TraceIdentifier,
            ct);

        if (!ok) return NotFound();
        return Ok(new { action = "deleted" });
    }

    /// <summary>绑定的审计历史。</summary>
    [HttpGet("bindings/{bindingId}/history")]
    public async Task<IActionResult> History(
        string inodeId, string bindingId,
        [FromQuery] DateTimeOffset? from,
        CancellationToken ct)
    {
        var binding = await _inodeEntities.GetBindingAsync(inodeId, bindingId, ct);
        if (binding is null) return Ok(Array.Empty<EntityHistoryDto>());

        var history = await _facade.GetHistoryAsync(inodeId, bindingId, from, ct);

        var result = history.Select(a => new EntityHistoryDto(
            a.AuditId,
            a.EntityId,
            binding.EntityType,
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

    private async Task<IActionResult> WriteAsync(
        string inodeId, string bindingId,
        Dictionary<string, JsonElement> values, bool replaceAll,
        CancellationToken ct)
    {
        var binding = await _inodeEntities.GetBindingAsync(inodeId, bindingId, ct);
        if (binding is null) return NotFound();

        var expected = ParseExpectedHeader();

        try
        {
            var typed = ConvertBody(binding.EntityType, values);

            if (replaceAll)
            {
                await _facade.SaveAsync(inodeId, bindingId, typed,
                    User.Identity?.Name ?? "system",
                    HttpContext.TraceIdentifier,
                    ct, expected);
            }
            else
            {
                await _facade.PatchAsync(inodeId, bindingId, typed,
                    User.Identity?.Name ?? "system",
                    HttpContext.TraceIdentifier,
                    ct, expected);
            }

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
    /// 返回该 iNode 下所有绑定的全部属性值（聚合 JSON）。
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

        // 单位不做换算：base 和 original 等价，都返回裸数字（存储值即输入值）
        var bareValue = unitsMode is "base" or "original";

        // ---- 2. 加载所有绑定实体（复用现有 Facade） ----
        var all = await _facade.LoadAllByInodeAsync(inodeId, ct);

        // ---- 3. 构建 entities 字典（key = entityType/entityId，同类型可多条） ----
        var entitiesDict = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var entity in all)
        {
            var entityType = entity.EntityType;
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

            entitiesDict[$"{entityType}/{entity.EntityId}"] = new
            {
                entityType,
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
