using System.Net.Security;
using System.Security.Claims;
using APromisedLand.Shared.Contracts;
using APromisedLand.Shared.DTOs.Overflow;
using APromisedLand.Shared.Interfaces;
using APromisedLand.Shared.MessageContracts;
using APromisedLand.Shared.Projects.Elasticsearch;
using APromisedLand.Shared.Projects.Elasticsearch.Embedding;
using Elastic.Clients.Elasticsearch;
using FastExpressionCompiler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.Models;
using QuestionService.Nats.Publishers;
using QuestionService.Services;
using Wolverine;
using IEmbeddingGenerator = QuestionService.Services.IEmbeddingGenerator;

namespace QuestionService.Controllers;

[ApiController]
[Route("[controller]")]
// public partial class QuestionsController(QuestionDbContext db, IMessageBus bus, 
public partial class QuestionsController(QuestionDbContext db,
    TagService tagService, IQuestionPublisher publisher, 
    IEmbeddingGenerator embedder, ElasticsearchClient elasticClient,
    ILogger<QuestionsController> logger) : ControllerBase
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
            return BadRequest("含有无效的标签。");

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

        // await bus.PublishAsync(new QuestionCreated(question.Id, question.Title, question.Content,
        //     question.CreatedAt, question.TagSlugs));
        
        var data = MapToQuestionData(question);
        await publisher.PublishQuestionAsync(data, "create");

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

    // [HttpGet("{id}")]
    // public async Task<ActionResult<Question>> GetQuestion(string id)
    // {
    //     var question = await db.Questions.FindAsync(id);
    //
    //     if (question is null) return NotFound();
    //
    //     await db.Questions.Where(x => x.Id == id)
    //         .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ViewCount,
    //             x => x.ViewCount + 1));
    //
    //     return question;
    // }

    [HttpGet("{id}")]
    public async Task<ActionResult<Question>> GetQuestion(string id)
    {
        var question = await db.Questions
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == id);
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

        // await bus.PublishAsync(new QuestionUpdated(question.Id, question.Title, question.Content,
        //     question.TagSlugs.AsArray()));

        var data = MapToQuestionData(question);
        await publisher.PublishQuestionAsync(data, "update");
        
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

        // await bus.PublishAsync(new QuestionDeleted(question.Id));
        
        var data = MapToQuestionData(question);
        await publisher.PublishQuestionAsync(data, "delete");

        return NoContent();
    }

    [HttpGet("errors")]
    public ActionResult GetErrorsResponsed(int code)
    {
        ModelState.AddModelError("问题一", "验证问题一");
        ModelState.AddModelError("问题二", "验证问题二");

        return code switch
        {
            400 => BadRequest("与正当请求完全相反。"),
            401 => Unauthorized(),
            403 => Forbid(),
            404 => NotFound(),
            500 => throw new Exception("这是一个服务器错误。"),
            _ => ValidationProblem(ModelState)
        };
    }

    private static QuestionData MapToQuestionData(Question question)
    {
        return new QuestionData
        {
            Id = question.Id,
            Title = question.Title,
            Content = question.Content,
            CreatedAt = question.CreatedAt,
            Tags = question.TagSlugs,
        };
    }
}