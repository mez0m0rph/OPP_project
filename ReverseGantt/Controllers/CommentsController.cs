using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Models;

namespace ReverseGantt.Controllers;

[ApiController]
[Route("api/comments")]
public class CommentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CommentsController(AppDbContext db)
    {
        _db = db;
    }

    public record CommentDto(int Id, int ProjectId, int? TaskItemId, int AuthorId, string Text, DateTime CreatedAt);
    public record CreateCommentRequest(int ProjectId, int? TaskItemId, int AuthorId, string Text);

    [HttpGet]
    public async Task<ActionResult<List<CommentDto>>> Get([FromQuery] int projectId, [FromQuery] int? taskItemId)
    {
        var q = _db.Comments.AsNoTracking().Where(c => c.ProjectId == projectId);
        if (taskItemId.HasValue) q = q.Where(c => c.TaskItemId == taskItemId.Value);

        var items = await q.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return items.Select(c => new CommentDto(c.Id, c.ProjectId, c.TaskItemId, c.AuthorId, c.Text, c.CreatedAt)).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<CommentDto>> Create([FromBody] CreateCommentRequest req)
    {
        var proj = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProjectId);
        if (proj == null) return BadRequest("Project not found");

        if (req.TaskItemId.HasValue)
        {
            var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TaskItemId.Value);
            if (task == null || task.ProjectId != req.ProjectId) return BadRequest("Task not found in project");
        }

        var author = await _db.Participants.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.AuthorId);
        if (author == null) return BadRequest("Author not found");

        var c = new Comment
        {
            ProjectId = req.ProjectId,
            TaskItemId = req.TaskItemId,
            AuthorId = req.AuthorId,
            Text = req.Text.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(c);
        await _db.SaveChangesAsync();

        return Ok(new CommentDto(c.Id, c.ProjectId, c.TaskItemId, c.AuthorId, c.Text, c.CreatedAt));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Comments.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        _db.Comments.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
