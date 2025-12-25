using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Models;

namespace ReverseGantt.Controllers;

[ApiController]
[Route("api/task-results")]
public class TaskResultsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TaskResultsController(AppDbContext db)
    {
        _db = db;
    }

    int? CurrentParticipantId()
    {
        var v = User.FindFirstValue("participantId");
        if (string.IsNullOrWhiteSpace(v)) return null;
        if (int.TryParse(v, out var id)) return id;
        return null;
    }

    public record TaskResultDto(int Id, int TaskItemId, int AuthorId, string? ResultText, string? ResultUrl, DateTime SubmittedAt);
    public record UpsertRequest(int TaskItemId, string? ResultText, string? ResultUrl);

    [HttpGet]
    [Authorize(Roles = "Teacher,Captain,Participant")]
    public async Task<ActionResult<TaskResultDto>> Get([FromQuery] int taskItemId, CancellationToken ct)
    {
        var r = await _db.TaskResults.AsNoTracking().FirstOrDefaultAsync(x => x.TaskItemId == taskItemId, ct);
        if (r == null) return Ok(null);
        return Ok(new TaskResultDto(r.Id, r.TaskItemId, r.AuthorId, r.ResultText, r.ResultUrl, r.SubmittedAt));
    }

    [HttpPost]
    [Authorize(Roles = "Captain,Participant")]
    public async Task<ActionResult<TaskResultDto>> Upsert([FromBody] UpsertRequest req, CancellationToken ct)
    {
        var authorId = CurrentParticipantId();
        if (!authorId.HasValue) return Forbid();

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == req.TaskItemId, ct);
        if (task == null) return BadRequest("Task not found");

        var existing = await _db.TaskResults.FirstOrDefaultAsync(x => x.TaskItemId == req.TaskItemId, ct);

        var text = string.IsNullOrWhiteSpace(req.ResultText) ? null : req.ResultText.Trim();
        var url = string.IsNullOrWhiteSpace(req.ResultUrl) ? null : req.ResultUrl.Trim();

        if (existing == null)
        {
            var r = new TaskResult
            {
                TaskItemId = req.TaskItemId,
                AuthorId = authorId.Value,
                ResultText = text,
                ResultUrl = url,
                SubmittedAt = DateTime.UtcNow
            };

            _db.TaskResults.Add(r);
            await _db.SaveChangesAsync(ct);

            return Ok(new TaskResultDto(r.Id, r.TaskItemId, r.AuthorId, r.ResultText, r.ResultUrl, r.SubmittedAt));
        }

        existing.AuthorId = authorId.Value;
        existing.ResultText = text;
        existing.ResultUrl = url;
        existing.SubmittedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new TaskResultDto(existing.Id, existing.TaskItemId, existing.AuthorId, existing.ResultText, existing.ResultUrl, existing.SubmittedAt));
    }
}
