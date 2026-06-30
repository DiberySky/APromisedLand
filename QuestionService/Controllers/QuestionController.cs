using System.Security.Claims;
using APromisedLand.Shared.Contracts;
using APromisedLand.Shared.DTOs.Overflow;
using FastExpressionCompiler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.Models;
using QuestionService.Services;
using Wolverine;

namespace QuestionService.Controllers;

[ApiController]
[Route("[controller]")]
public class QuestionController(QuestionDbContext db, IMessageBus bus, TagService tagService) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto dto)
    {
        // var validTags = await db.Tags.Where(x => dto.Tags.Contains(x.Slug)).ToListAsync();
        //
        // var missing = dto.Tags.Except(validTags.Select(tag => tag.Slug)).ToList();
        //
        // if (missing.Count != 0)
        //     return BadRequest(string.Join(", ", missing));

        if (!await tagService.AreTagsValidAsync(dto.Tags))
            return BadRequest("标签无效。");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.FindFirstValue("name");

        if (userId is null || userName is null)
        {
            return BadRequest("无法获取用户详细信息。");
        }

        var question = new Question
        {
            Title = dto.Title,
            Content = dto.Content,
            TagSlugs = dto.Tags,
            AskerId = userId,
            AskerDisplayName = userName,
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync();

        await bus.PublishAsync(new QuestionCreated(question.Id, question.Title, question.Content,
            question.CreatedAt, question.TagSlugs));

        return Ok(question);
    }

    [HttpGet]
    public async Task<ActionResult<List<Question>>> GetQuestions(string? tag)
    {
        var query = db.Questions.AsQueryable();

        if (!string.IsNullOrEmpty(tag))
        {
            query = query.Where(x => x.TagSlugs.Contains(tag));
        }

        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Question>> GetQuestion(string id)
    {
        var question = await db.Questions.FindAsync(id);

        if (question is null) return NotFound();

        await db.Questions.Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ViewCount,
                x => x.ViewCount + 1));

        return question;
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateQuestion(string id, CreateQuestionDto dto)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Forbid();

        // var validTags = await db.Tags.Where(x => dto.Tags.Contains(x.Slug)).ToListAsync();
        //
        // var missing = dto.Tags.Except(validTags.Select(tag => tag.Slug)).ToList();
        //
        // if (missing.Count != 0)
        //     return BadRequest(string.Join(", ", missing));

        if (!await tagService.AreTagsValidAsync(dto.Tags))
            return BadRequest("标签无效。");

        question.Title = dto.Title;
        question.Content = dto.Content;
        question.TagSlugs = dto.Tags;
        question.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        await bus.PublishAsync(new QuestionUpdated(question.Id, question.Title, question.Content,
            question.TagSlugs.AsArray()));

        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteQuestion(string id)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != question.AskerId) return Forbid();

        db.Questions.Remove(question);
        await db.SaveChangesAsync();

        await bus.PublishAsync(new QuestionDeleted(question.Id));
        
        return NoContent();
    }
}