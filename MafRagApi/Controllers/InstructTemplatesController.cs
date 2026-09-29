using MafRagApi.Models;
using MafRagApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MafRagApi.Controllers;

/// <summary>
/// 指令模板管理。
/// 路由前缀：/api/vllm/chat/instruct/templates
/// </summary>
[ApiController]
[Route("api/vllm/chat/instruct/templates")]
[Produces("application/json")]
public sealed class InstructTemplatesController(
    IInstructionTemplateStore store,
    ILogger<InstructTemplatesController> logger) : ControllerBase
{
    /// <summary>列出全部模板。</summary>
    [HttpGet]
    public ActionResult<InstructionTemplateListDto> List()
        => Ok(new InstructionTemplateListDto { Items = store.List() });

    /// <summary>获取单个模板。</summary>
    [HttpGet("{id}")]
    public ActionResult<InstructionTemplate> Get(string id)
        => store.TryGet(id, out var tpl)
            ? Ok(tpl)
            : NotFound(new { error = $"template '{id}' not found." });

    /// <summary>新增或覆盖一个模板（同 id 覆盖）。</summary>
    [HttpPost]
    public IActionResult Upsert([FromBody] InstructionTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Id))
            return BadRequest(new { error = "id is required." });
        if (string.IsNullOrWhiteSpace(template.Instruction))
            return BadRequest(new { error = "instruction is required." });

        // 允许覆盖：先删再加
        store.TryRemove(template.Id);
        store.TryAdd(template);

        logger.LogInformation("Template upserted: id={Id}", template.Id);

        return Ok(template);
    }

    /// <summary>删除一个模板。</summary>
    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
        => store.TryRemove(id)
            ? NoContent()
            : NotFound(new { error = $"template '{id}' not found." });
}