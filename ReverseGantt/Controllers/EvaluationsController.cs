using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Models;

namespace ReverseGantt.Controllers;

[ApiController]
[Route("api/evaluations")]
[Authorize(Roles = "Teacher")]
public class EvaluationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EvaluationsController(AppDbContext db)
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

    public record EvaluationDto(int Id, int TaskItemId, int TeacherId, int? Score, string? Feedback, DateTime UpdatedAt);
    public record UpsertRequest(int TaskItemId, int? Score, string? Feedback);

    [HttpGet]
    public async Task<ActionResult<EvaluationDto>> Get([FromQuery] int taskItemId, CancellationToken ct)
    {
        var e = await _db.TaskEvaluations.AsNoTracking().FirstOrDefaultAsync(x => x.TaskItemId == taskItemId, ct);
        if (e == null) return Ok(null);
        return Ok(new EvaluationDto(e.Id, e.TaskItemId, e.TeacherId, e.Score, e.Feedback, e.UpdatedAt));
    }

    [HttpPost]
    public async Task<ActionResult<EvaluationDto>> Upsert([FromBody] UpsertRequest req, CancellationToken ct)
    {
        var teacherId = CurrentParticipantId();
        if (!teacherId.HasValue) return Forbid();

        var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TaskItemId, ct);
        if (task == null) return BadRequest("Task not found");

        if (req.Score.HasValue && (req.Score.Value < 0 || req.Score.Value > 100))
            return BadRequest("Score must be 0..100");

        var existing = await _db.TaskEvaluations.FirstOrDefaultAsync(x => x.TaskItemId == req.TaskItemId, ct);

        if (existing == null)
        {
            var e = new TaskEvaluation
            {
                TaskItemId = req.TaskItemId,
                TeacherId = teacherId.Value,
                Score = req.Score,
                Feedback = string.IsNullOrWhiteSpace(req.Feedback) ? null : req.Feedback.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.TaskEvaluations.Add(e);
            await _db.SaveChangesAsync(ct);

            return Ok(new EvaluationDto(e.Id, e.TaskItemId, e.TeacherId, e.Score, e.Feedback, e.UpdatedAt));
        }

        existing.TeacherId = teacherId.Value;
        existing.Score = req.Score;
        existing.Feedback = string.IsNullOrWhiteSpace(req.Feedback) ? null : req.Feedback.Trim();
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new EvaluationDto(existing.Id, existing.TaskItemId, existing.TeacherId, existing.Score, existing.Feedback, existing.UpdatedAt));
    }
}
