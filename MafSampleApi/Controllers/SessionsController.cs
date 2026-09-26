using MafSampleApi.Models;
using Microsoft.AspNetCore.Mvc;
using MafSampleApi.Services;

namespace MafSampleApi.Controllers;

[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(ISessionStore store) : ControllerBase
{
    /// <summary>列出所有当前活跃的会话 ID（按最近访问时间倒序）。</summary>
    [HttpGet]
    public ActionResult<SessionListDto> List()
        => Ok(new SessionListDto { Ids = store.ListIds() });

    /// <summary>清空指定会话历史。</summary>
    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
        => store.TryRemove(id)
            ? NoContent()
            : NotFound(new { error = $"session '{id}' not found." });

    /// <summary>清空全部会话。</summary>
    [HttpDelete]
    public IActionResult Clear()
    {
        store.Clear();
        return NoContent();
    }
}