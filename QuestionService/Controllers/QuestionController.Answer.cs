using System.Security.Claims;
using APromisedLand.Shared.DTOs.Overflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestionService.Models;
using APromisedLand.Shared.Contracts;

namespace QuestionService.Controllers;

public partial class QuestionsController
{
    // [Authorize]
    // [HttpPost("{questionId}/answers")]
    // public async Task<ActionResult> PostAnswer(string questionId, CreateAnswerDto dto)
    // {
    //     var question = await db.Questions.FindAsync(questionId);
    //     if (question is null) return NotFound();
    //     var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    //     var name = User.FindFirstValue("name");
    //     if (userId is null || name is null) return BadRequest("Cannot get user details");
    //     var answer = new Answer
    //     {
    //         Content = dto.Content,
    //         UserId = userId,
    //         UserDisplayName = name,
    //         QuestionId = questionId
    //     };
    //     
    //     question.Answers.Add(answer);
    //     question.AnswerCount++;
    //     
    //     await db.SaveChangesAsync();
    //     return Created($"/questions/{questionId}", answer);
    // }
    //
    [Authorize]
    [HttpPut("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> UpdateAnswer(string questionId, string answerId,
        CreateAnswerDto dto)
    {
        var answer = await db.Answers.FindAsync(answerId);
        if (answer is null) return NotFound();
        if (answer.QuestionId != questionId) return BadRequest("无法更新答案详情。");
        
        answer.Content = dto.Content;
        answer.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();
        
        return NoContent();
    }
    
    // [Authorize]
    // [HttpDelete("{questionId}/answers/{answerId}")]
    // public async Task<ActionResult> DeleteAnswer(string questionId, string answerId)
    // {
    //     var answer = await db.Answers.FindAsync(answerId);
    //     var question = await db.Questions.FindAsync(questionId);
    //     if (answer is null || question is null) return NotFound();
    //     if (answer.QuestionId != questionId || answer.Accepted) return BadRequest("无法删除此答案。");
    //     db.Answers.Remove(answer);
    //     question.AnswerCount--;
    //     await db.SaveChangesAsync();
    //     await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));
    //     return NoContent();
    // }
    
    // [Authorize]
    // [HttpPost("questions/{questionId}/answer/{answerId}/accept")]
    // public async Task<ActionResult> AcceptAnswer(string questionId, string answerId)
    // {
    //     var answer = await db.Answers.FindAsync(answerId);
    //     var question = await db.Questions.FindAsync(questionId);
    //     if (answer is null || question is null) return NotFound();
    //     if (answer.QuestionId != questionId || question.HasAcceptedAnswer) return
    //         BadRequest("Cannot accept answer");
    //     answer.Accepted = true;
    //     question.HasAcceptedAnswer = true;
    //     await db.SaveChangesAsync();
    //     return NoContent();
    // }
    
    [Authorize]
    [HttpPost("{questionId}/answers")]
    public async Task<ActionResult> PostAnswer(string questionId, CreateAnswerDto dto)
    {
        var question = await db.Questions.FindAsync(questionId);
        if (question is null) return NotFound();
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");
        
        if (userId is null || name is null) return BadRequest("无法获取用户详情。");
        
        var answer = new Answer
        {
            Content = dto.Content,
            UserId = userId,
            UserDisplayName = name,
            QuestionId = questionId
        };
        
        question.Answers.Add(answer);
        question.AnswerCount++;
        
        await db.SaveChangesAsync();
        
        await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));
        
        return Created($"/questions/{questionId}", answer);
    }
    
    [Authorize]
    [HttpDelete("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> DeleteAnswer(string questionId, string answerId)
    {
        var answer = await db.Answers.FindAsync(answerId);
        var question = await db.Questions.FindAsync(questionId);
        if (answer is null || question is null) return NotFound();
        if (answer.QuestionId != questionId) return BadRequest("无法删除答案。");
        
        db.Answers.Remove(answer);
        question.AnswerCount--;
        
        await db.SaveChangesAsync();
        
        await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));
        
        return NoContent();
    }
    
    [Authorize]
    [HttpPost("{questionId}/answers/{answerId}/accept")]
    public async Task<ActionResult> AcceptAnswer(string questionId, string answerId)
    {
        var answer = await db.Answers.FindAsync(answerId);
        var question = await db.Questions.FindAsync(questionId);
        if (answer is null || question is null) return NotFound();
        if (answer.QuestionId != questionId || question.HasAcceptedAnswer) return
            BadRequest("无法接受这个答案。");
        
        answer.Accepted = true;
        question.HasAcceptedAnswer = true;
        
        await db.SaveChangesAsync();
        
        await bus.PublishAsync(new AnswerAccepted(questionId));
        
        return NoContent();
    }
    
    
}