using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.StringTreeSky.Services;
using TreeGraph.StringTree.Contracts;

namespace TreeGraph.Api.StringTreeSky.Controllers;

[ApiController]
[Route("api/string-tree/spaces")]
public class SpaceController : ControllerBase
{
    private readonly IStringTreeService _tree;
    private readonly EavDbContext _eavDb;

    public SpaceController(IStringTreeService tree, EavDbContext eavDb)
    {
        _tree = tree;
        _eavDb = eavDb;
    }

    // ============ List ============

    [HttpGet]
    public async Task<ActionResult<StringTreeResponse<List<SpaceDto>>>> List(CancellationToken ct)
    {
        var roots = await _tree.GetRootNodesAsync(ct);
        var spaces = roots.Select(ToSpaceDto).ToList();
        return Ok(StringTreeResponse<List<SpaceDto>>.Ok(spaces));
    }

    // ============ Get ============

    [HttpGet("{id}")]
    public async Task<ActionResult<StringTreeResponse<SpaceDto>>> Get(string id, CancellationToken ct)
    {
        var node = await _tree.GetNodeAsync(id, ct);
        if (node is null || node.ParentId is not null)
            return NotFound(StringTreeResponse<SpaceDto>.Fail("空间不存在"));

        return Ok(StringTreeResponse<SpaceDto>.Ok(ToSpaceDto(node)));
    }

    // ============ Create ============

    [HttpPost]
    public async Task<ActionResult<StringTreeResponse<SpaceDto>>> Create(
        [FromBody] SpaceDto dto,
        [FromServices] IEntityTypeTemplateService templateSvc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(StringTreeResponse<SpaceDto>.Fail("空间名称不能为空"));

        var newId = Guid.NewGuid().ToString("D");

        // 决定 EntityType
        string entityType;
        if (string.IsNullOrWhiteSpace(dto.EntityType) || dto.EntityType == "auto")
        {
            // 默认：独立
            entityType = $"{StringTreeEntityTypes.Node}:{newId}";
        }
        else
        {
            // 用户显式指定（"StringTreeNode" 共享 / "StringTreeNode:{other}" 复用）
            entityType = dto.EntityType;
        }

        // 独立 EntityType 需在 catalog 注册
        if (entityType != StringTreeEntityTypes.Node)
        {
            await EnsureEntityTypeRegisteredAsync(
                entityType, dto.Name.Trim(), dto.Description, ct);
        }

        // 从模板复制属性（仅独立 EntityType 时；复制失败不阻断建空间，结果仅报告）
        if (entityType != StringTreeEntityTypes.Node
            && !string.IsNullOrWhiteSpace(dto.TemplateEntityType))
        {
            await templateSvc.CopyAttributesAsync(
                dto.TemplateEntityType!, entityType, ct);
        }

        var created = await _tree.CreateNodeAsync(new StringNodeDto
        {
            Id = newId,
            Name = dto.Name.Trim(),
            Description = dto.Description,
            ParentId = null,
            EntityType = entityType
        }, ct);

        return Ok(StringTreeResponse<SpaceDto>.Ok(ToSpaceDto(created)));
    }

    // ============ Update ============

    [HttpPut("{id}")]
    public async Task<ActionResult<StringTreeResponse<SpaceDto>>> Update(
        string id, [FromBody] SpaceDto dto, CancellationToken ct)
    {
        var existing = await _tree.GetNodeAsync(id, ct);
        if (existing is null || existing.ParentId is not null)
            return NotFound(StringTreeResponse<SpaceDto>.Fail("空间不存在"));

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(StringTreeResponse<SpaceDto>.Fail("空间名称不能为空"));

        existing.Name = dto.Name.Trim();
        existing.Description = dto.Description;
        // EntityType 不允许通过 Update 变更（避免破坏已写入的属性数据）

        var updated = await _tree.UpdateNodeAsync(id, existing, ct);
        if (updated is null)
            return NotFound(StringTreeResponse<SpaceDto>.Fail("空间不存在"));

        // 若 catalog 中已有该 EntityType，同步显示名
        await SyncEntityTypeDisplayNameAsync(updated.EntityType, updated.Name, ct);

        return Ok(StringTreeResponse<SpaceDto>.Ok(ToSpaceDto(updated)));
    }

    // ============ Delete ============

    [HttpDelete("{id}")]
    public async Task<ActionResult<StringTreeResponse<bool>>> Delete(string id, CancellationToken ct)
    {
        var existing = await _tree.GetNodeAsync(id, ct);
        if (existing is null || existing.ParentId is not null)
            return NotFound(StringTreeResponse<bool>.Fail("空间不存在"));

        var entityType = existing.EntityType;

        var ok = await _tree.DeleteNodeAsync(id, ct);
        if (!ok) return NotFound(StringTreeResponse<bool>.Fail("删除失败"));

        // 独立 EntityType 清理（若无其他空间引用）
        if (entityType != StringTreeEntityTypes.Node)
            await TryCleanupEntityTypeAsync(entityType, ct);

        return Ok(StringTreeResponse<bool>.Ok(true));
    }

    // ============ 内部 ============

    private async Task EnsureEntityTypeRegisteredAsync(
        string entityType, string displayName, string? description, CancellationToken ct)
    {
        var exists = await _eavDb.EntityTypes
            .AnyAsync(t => t.EntityType == entityType && !t.IsDeleted, ct);
        if (exists) return;

        _eavDb.EntityTypes.Add(new EntityTypeDefinition
        {
            EntityTypeId = Guid.NewGuid().ToString("D"),
            EntityType = entityType,
            DisplayName = displayName,
            Description = description,
            DisplayOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _eavDb.SaveChangesAsync(ct);
    }

    private async Task SyncEntityTypeDisplayNameAsync(
        string entityType, string displayName, CancellationToken ct)
    {
        if (entityType == StringTreeEntityTypes.Node) return;

        var cat = await _eavDb.EntityTypes
            .FirstOrDefaultAsync(t => t.EntityType == entityType && !t.IsDeleted, ct);
        if (cat is null) return;

        cat.DisplayName = displayName;
        cat.UpdatedAt = DateTimeOffset.UtcNow;
        await _eavDb.SaveChangesAsync(ct);
    }

    private async Task TryCleanupEntityTypeAsync(string entityType, CancellationToken ct)
    {
        // 检查是否还有空间（或其他节点）引用
        var stillInUse = await _eavDb.StringTreeSkyNodes
            .AnyAsync(n => n.EntityType == entityType, ct);
        if (stillInUse) return;

        var cat = await _eavDb.EntityTypes
            .FirstOrDefaultAsync(t => t.EntityType == entityType, ct);
        if (cat is null) return;

        cat.IsDeleted = true;
        cat.UpdatedAt = DateTimeOffset.UtcNow;
        await _eavDb.SaveChangesAsync(ct);
    }

    private static SpaceDto ToSpaceDto(StringNodeDto node) => new()
    {
        Id = node.Id,
        Name = node.Name,
        Description = node.Description,
        EntityType = string.IsNullOrEmpty(node.EntityType)
            ? StringTreeEntityTypes.Node
            : node.EntityType
    };
}
