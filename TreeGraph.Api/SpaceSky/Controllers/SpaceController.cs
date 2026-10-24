using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.SpaceSky.Entities;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.SpaceSky.Controllers;

/// <summary>
/// 空间 CRUD API：路由 /Space
/// </summary>
[ApiController]
[Route("[controller]")]
public class SpaceController(TreeGraphDbContext db, IEventPublisher publisher) : ControllerBase
{
    // GET /Space
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await db.Spaces
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken));

    // GET /Space/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var space = await db.Spaces.FindAsync([id], cancellationToken);
        return space == null
            ? NotFound(new { error = $"空间 '{id}' 不存在" })
            : Ok(space);
    }

    // POST /Space
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Space space, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(space.Name))
            return BadRequest(new { error = "名称不能为空" });

        if (string.IsNullOrEmpty(space.Id))
            space.Id = Guid.NewGuid().ToString();

        db.Spaces.Add(space);

        // 同步创建对应的空间树根节点：root id = space id；名称存 nodes 注册表（R2 Name 收编）
        db.Nodes.Add(new TreeGraph.Shared.NodeSky.Entities.Node
        {
            Id = space.Id,
            HostType = "SpaceTree",
            Name = space.Name,
        });
        db.SpaceTrees.Add(new SpaceTree
        {
            Id = space.Id,
            Description = space.Description ?? "",
            ParentId = null,
            CanHaveChildren = true,
            SortOrder = 0,
            HasChildren = false
        });

        await db.SaveChangesAsync(cancellationToken);

        // ★ 语义搜索：空间根节点新增 → 即时同步向量（消费端幂等；根节点 isSpaceNode 无节点向量，仅占位）
        await publisher.Publish(new InodeChangedEvent
        {
            InodeId = space.Id,
            Kind = InodeChangeKind.Saved,
            PathAffected = false
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = space.Id }, space);
    }

    // PUT /Space/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Space input, CancellationToken cancellationToken)
    {
        var space = await db.Spaces.FindAsync([id], cancellationToken);
        if (space == null)
            return NotFound(new { error = $"空间 '{id}' 不存在" });

        if (string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { error = "名称不能为空" });

        space.Name = input.Name;
        space.Description = input.Description;
        space.Type = input.Type;

        // 同步空间树根节点：root id = space id；名称存 nodes 注册表（R2 Name 收编）
        var root = await db.SpaceTrees.FindAsync([id], cancellationToken);
        if (root != null)
        {
            var rootNode = await db.Nodes.FindAsync([id], cancellationToken);
            if (rootNode != null) rootNode.Name = input.Name;
        }
        else
        {
            db.Nodes.Add(new TreeGraph.Shared.NodeSky.Entities.Node
            {
                Id = space.Id,
                HostType = "SpaceTree",
                Name = space.Name,
            });
            db.SpaceTrees.Add(new SpaceTree
            {
                Id = space.Id,
                Description = space.Description ?? "",
                ParentId = null,
                CanHaveChildren = true,
                SortOrder = 0,
                HasChildren = false
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        // ★ 语义搜索：空间改名 → 根节点名即后代 path 第一段，全部后代路径型向量失效，需级联刷新
        await publisher.Publish(new InodeChangedEvent
        {
            InodeId = id,
            Kind = InodeChangeKind.Saved,
            PathAffected = true
        }, cancellationToken);

        return Ok(space);
    }

    // DELETE /Space/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var space = await db.Spaces.FindAsync([id], cancellationToken);
        if (space == null)
            return NotFound(new { error = $"空间 '{id}' 不存在" });

        // 级联规则：空间树根节点有子节点时禁止删除；无子节点则连同根节点一并删除
        var root = await db.SpaceTrees.FindAsync([id], cancellationToken);
        if (root != null)
        {
            var hasChildren = await db.SpaceTrees.AnyAsync(n => n.ParentId == id, cancellationToken);
            if (hasChildren)
                return Conflict(new { error = "空间树存在子节点，无法删除" });

            // 引用为空方可删除本项：仍有边引用根节点时拒绝（不采用级联删除）
            var hasEdges = await db.EdgeInstances
                .AnyAsync(e => e.FromNodeId == id || e.ToNodeId == id, cancellationToken);
            if (hasEdges)
                return Conflict(new { error = "空间根节点仍被边引用，无法删除。请先删除引用该节点的边。" });

            // 先清空自身数据才允许删本项：根节点仍有挂载实体数据时拒绝（不采用级联删除）
            var hasEntities = await db.InodeEntities
                .AnyAsync(m => m.InodeId == id, cancellationToken);
            if (hasEntities)
                return Conflict(new { error = "空间根节点仍有挂载的实体数据，无法删除。请先清空其全部实体数据。" });

            // 类型声明（inode_entitytypes）不含业务数据，随宿主一并清理，避免孤儿声明
            var declarations = await db.InodeEntityTypes
                .Where(x => x.InodeId == id)
                .ToListAsync(cancellationToken);
            db.InodeEntityTypes.RemoveRange(declarations);

            db.SpaceTrees.Remove(root);

            // 同步注销 nodes 注册表行（R2 Name 收编：先宿主后注册表）
            var rootNode = await db.Nodes.FindAsync([id], cancellationToken);
            if (rootNode != null) db.Nodes.Remove(rootNode);
        }

        db.Spaces.Remove(space);
        await db.SaveChangesAsync(cancellationToken);

        // ★ 语义搜索：空间根节点已删除 → 消费端据 InodeId 清除残留向量
        await publisher.Publish(new InodeChangedEvent
        {
            InodeId = id,
            Kind = InodeChangeKind.Deleted,
            PathAffected = false
        }, cancellationToken);

        return Ok(true);
    }
}
