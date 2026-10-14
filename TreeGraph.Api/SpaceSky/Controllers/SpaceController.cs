using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.SpaceSky.Entities;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.SpaceSky.Controllers;

/// <summary>
/// 空间 CRUD API：路由 /Space
/// </summary>
[ApiController]
[Route("[controller]")]
public class SpaceController(TreeGraphDbContext db) : ControllerBase
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

        // 同步创建对应的空间树根节点：root id = space id，root name = space name
        db.SpaceTrees.Add(new SpaceTree
        {
            Id = space.Id,
            Name = space.Name,
            Description = space.Description ?? "",
            ParentId = null,
            CanHaveChildren = true,
            SortOrder = 0,
            HasChildren = false
        });

        await db.SaveChangesAsync(cancellationToken);
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

        // 同步空间树根节点：root id = space id；不存在则自愈补建（与创建路径一致，同一次 SaveChanges 原子提交）
        var root = await db.SpaceTrees.FindAsync([id], cancellationToken);
        if (root != null)
        {
            root.Name = input.Name;
        }
        else
        {
            db.SpaceTrees.Add(new SpaceTree
            {
                Id = space.Id,
                Name = space.Name,
                Description = space.Description ?? "",
                ParentId = null,
                CanHaveChildren = true,
                SortOrder = 0,
                HasChildren = false
            });
        }

        await db.SaveChangesAsync(cancellationToken);
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

            db.SpaceTrees.Remove(root);
        }

        db.Spaces.Remove(space);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }
}
