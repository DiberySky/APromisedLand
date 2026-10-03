using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>创建实体类型（POST api/eav/entity-types）</summary>
public class CreateEntityTypeRequest
{
    /// <summary>
    /// 可选。不传时服务端自动生成 `et_` + 12 位 hex（如 et_3f9a2b1c8d4e）。
    /// 保留字段用于脚本 / 工具导入时指定标识。
    /// </summary>
    public string? EntityType { get; set; }

    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>更新实体类型（PUT api/eav/entity-types/{id}）</summary>
public class UpdateEntityTypeRequest
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
}

/// <summary>实体类型详情（GET api/eav/entity-types/{id}）</summary>
public record EntityTypeDetailDto(
    string EntityTypeId,
    string EntityType,
    string DisplayName,
    string? Description,
    int DisplayOrder,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int AttributeCount);
