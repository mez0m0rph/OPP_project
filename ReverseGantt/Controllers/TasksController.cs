using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Interfaces;
using ReverseGantt.Models;
using ReverseGantt.Services;

namespace ReverseGantt.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IExecutorService _executorService;
    private readonly DependencyRulesService _rules;

    public TasksController(AppDbContext db, IExecutorService executorService, DependencyRulesService rules)
    {
        _db = db;
        _executorService = executorService;
        _rules = rules;
    }

    int? CurrentParticipantId()
    {
        var v = User.FindFirstValue("participantId");
        if (string.IsNullOrWhiteSpace(v)) return null;
        if (int.TryParse(v, out var id)) return id;
        return null;
    }

    string CurrentRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? "Participant";
    }

    bool IsPrivileged()
    {
        var r = CurrentRole();
        return r == "Teacher" || r == "Admin" || r == "Captain";
    }

    static DateTime ToUtc(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc) return dt;
        if (dt.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return dt.ToUniversalTime();
    }

    static DateTime? ToUtc(DateTime? dt) => dt.HasValue ? ToUtc(dt.Value) : null;

    public record TaskDto(
        int Id,
        int ProjectId,
        string Title,
        string? Description,
        DateTime? StartDate,
        DateTime? EndDate,
        DateTime Deadline,
        int? DurationDays,
        Status Status,
        List<int> ExecutorIds
    );

    public record CreateTaskRequest(
        int ProjectId,
        string Title,
        string? Description,
        DateTime? StartDate,
        DateTime? EndDate,
        DateTime Deadline,
        int? DurationDays,
        List<int>? ExecutorIds
    );

    public record UpdateTaskRequest(
        string Title,
        string? Description,
        DateTime? StartDate,
        DateTime? EndDate,
        DateTime Deadline,
        int? DurationDays
    );

    public record UpdateExecutorsRequest(List<int> ExecutorIds);
    public record UpdateStatusRequest(Status Status);

    public record BlockersResponse(
        List<DependencyRulesService.Blocker> StartBlockers,
        List<DependencyRulesService.Blocker> FinishBlockers
    );

    async Task<bool> EnsureSameTeamAsync(int projectId, CancellationToken ct)
    {
        if (IsPrivileged()) return true;

        var pid = CurrentParticipantId();
        if (!pid.HasValue) return false;

        var participant = await _db.Participants.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid.Value, ct);
        if (participant == null) return false;

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return false;

        return participant.TeamId == project.TeamId;
    }

    async Task<bool> CanActOnTaskAsParticipantAsync(TaskItem task, CancellationToken ct)
    {
        if (IsPrivileged()) return true;

        var pid = CurrentParticipantId();
        if (!pid.HasValue) return false;

        var participant = await _db.Participants.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid.Value, ct);
        if (participant == null) return false;

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == task.ProjectId, ct);
        if (project == null) return false;

        if (participant.TeamId != project.TeamId) return false;

        var hasAssignments = await _db.TaskAssignments.AsNoTracking().AnyAsync(a => a.TaskItemId == task.Id, ct);
        if (!hasAssignments) return true;

        return await _db.TaskAssignments.AsNoTracking().AnyAsync(a => a.TaskItemId == task.Id && a.ParticipantId == pid.Value, ct);
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskDto>>> GetByProject([FromQuery] int projectId, CancellationToken ct)
    {
        if (!await EnsureSameTeamAsync(projectId, ct)) return Forbid();

        var tasks = await _db.Tasks.AsNoTracking()
            .Include(t => t.Assignments)
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Deadline)
            .ToListAsync(ct);

        return tasks.Select(t => new TaskDto(
            t.Id,
            t.ProjectId,
            t.Title,
            t.Description,
            t.StartDate,
            t.EndDate,
            t.Deadline,
            t.DurationDays,
            t.Status,
            t.Assignments.Select(a => a.ParticipantId).ToList()
        )).ToList();
    }

    [HttpGet("{id:int}/blockers")]
    public async Task<ActionResult<BlockersResponse>> GetBlockers(int id, CancellationToken ct)
    {
        var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task == null) return NotFound();

        if (!await CanActOnTaskAsParticipantAsync(task, ct)) return Forbid();

        var start = await _rules.GetStartBlockersAsync(id, ct);
        var finish = await _rules.GetFinishBlockersAsync(id, ct);

        return Ok(new BlockersResponse(start, finish));
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create([FromBody] CreateTaskRequest req, CancellationToken ct)
    {
        if (!IsPrivileged()) return Forbid();
        if (!await EnsureSameTeamAsync(req.ProjectId, ct)) return Forbid();

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProjectId, ct);
        if (project == null) return BadRequest("Project not found");

        var deadlineUtc = ToUtc(req.Deadline);
        var startUtc = ToUtc(req.StartDate);
        var endUtc = ToUtc(req.EndDate);

        try
        {
            TaskValidator.ValidateForCreateOrUpdate(project, deadlineUtc, startUtc, endUtc, req.DurationDays);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        var task = new TaskItem
        {
            ProjectId = req.ProjectId,
            Title = req.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            StartDate = startUtc,
            EndDate = endUtc,
            Deadline = deadlineUtc,
            DurationDays = req.DurationDays,
            Status = Status.InProgress
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);

        var executorIds = (req.ExecutorIds ?? new List<int>()).Distinct().ToList();
        if (executorIds.Count > 0)
        {
            var validIds = await _db.Participants.AsNoTracking()
                .Where(p => executorIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(ct);

            foreach (var pid in validIds.Distinct())
                _db.TaskAssignments.Add(new TaskAssignment { TaskItemId = task.Id, ParticipantId = pid });

            await _db.SaveChangesAsync(ct);
        }

        var saved = await _db.Tasks.AsNoTracking()
            .Include(t => t.Assignments)
            .FirstAsync(t => t.Id == task.Id, ct);

        return Ok(new TaskDto(
            saved.Id,
            saved.ProjectId,
            saved.Title,
            saved.Description,
            saved.StartDate,
            saved.EndDate,
            saved.Deadline,
            saved.DurationDays,
            saved.Status,
            saved.Assignments.Select(a => a.ParticipantId).ToList()
        ));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TaskDto>> Update(int id, [FromBody] UpdateTaskRequest req, CancellationToken ct)
    {
        if (!IsPrivileged()) return Forbid();

        var task = await _db.Tasks.Include(t => t.Assignments).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task == null) return NotFound();

        if (!await EnsureSameTeamAsync(task.ProjectId, ct)) return Forbid();

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == task.ProjectId, ct);
        if (project == null) return BadRequest("Project not found");

        var deadlineUtc = ToUtc(req.Deadline);
        var startUtc = ToUtc(req.StartDate);
        var endUtc = ToUtc(req.EndDate);

        try
        {
            TaskValidator.ValidateForCreateOrUpdate(project, deadlineUtc, startUtc, endUtc, req.DurationDays);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        task.Title = req.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        task.StartDate = startUtc;
        task.EndDate = endUtc;
        task.Deadline = deadlineUtc;
        task.DurationDays = req.DurationDays;

        await _db.SaveChangesAsync(ct);

        return Ok(new TaskDto(
            task.Id,
            task.ProjectId,
            task.Title,
            task.Description,
            task.StartDate,
            task.EndDate,
            task.Deadline,
            task.DurationDays,
            task.Status,
            task.Assignments.Select(a => a.ParticipantId).ToList()
        ));
    }

    [HttpPatch("{id:int}/executors")]
    public async Task<IActionResult> UpdateExecutors(int id, [FromBody] UpdateExecutorsRequest req, CancellationToken ct)
    {
        if (!IsPrivileged()) return Forbid();

        var task = await _db.Tasks.Include(t => t.Assignments).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task == null) return NotFound();

        if (!await EnsureSameTeamAsync(task.ProjectId, ct)) return Forbid();

        var desired = req.ExecutorIds.Distinct().ToHashSet();
        var current = task.Assignments.Select(a => a.ParticipantId).ToHashSet();

        var remove = current.Except(desired).ToList();
        var add = desired.Except(current).ToList();

        if (remove.Count > 0)
        {
            var toRemove = task.Assignments.Where(a => remove.Contains(a.ParticipantId)).ToList();
            _db.TaskAssignments.RemoveRange(toRemove);
        }

        if (add.Count > 0)
        {
            var valid = await _db.Participants.AsNoTracking()
                .Where(p => add.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(ct);

            foreach (var pid in valid)
                _db.TaskAssignments.Add(new TaskAssignment { TaskItemId = id, ParticipantId = pid });
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task == null) return NotFound();

        if (!await CanActOnTaskAsParticipantAsync(task, ct)) return Forbid();

        try
        {
            TaskValidator.EnsureStatusNotReverted(task.Status, req.Status);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        if (req.Status == Status.Done)
        {
            var (allowed, reason) = await _rules.CanMarkDoneAsync(id, ct);
            if (!allowed) return BadRequest(reason ?? "Blocked by dependencies");
        }

        task.Status = req.Status;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/execute")]
    public async Task<IActionResult> Execute(int id, CancellationToken ct)
    {
        var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task == null) return NotFound();

        if (!await CanActOnTaskAsParticipantAsync(task, ct)) return Forbid();

        try
        {
            await _executorService.ExecuteTaskAsync(id, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!IsPrivileged()) return Forbid();

        var t = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t == null) return NotFound();

        if (!await EnsureSameTeamAsync(t.ProjectId, ct)) return Forbid();

        _db.Tasks.Remove(t);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
