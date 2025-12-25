using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Models;
using ReverseGantt.Services;

namespace ReverseGantt.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProjectsController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateProjectRequest(string Name, string Subject, int TeamId, DateTime? Deadline);
    public record UpdateProjectRequest(string Name, string Subject, DateTime? Deadline);

    static DateTime? ToUtc(DateTime? dt)
    {
        if (!dt.HasValue) return null;
        var v = dt.Value;
        if (v.Kind == DateTimeKind.Utc) return v;
        if (v.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(v, DateTimeKind.Utc);
        return v.ToUniversalTime();
    }

    [HttpGet]
    public async Task<ActionResult<List<Project>>> GetAll([FromQuery] int? teamId)
    {
        var q = _db.Projects.AsNoTracking().AsQueryable();
        if (teamId.HasValue) q = q.Where(p => p.TeamId == teamId.Value);
        return await q.OrderBy(p => p.Id).ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Project>> Create([FromBody] CreateProjectRequest req)
    {
        var teamExists = await _db.Teams.AnyAsync(t => t.Id == req.TeamId);
        if (!teamExists) return BadRequest("Team not found");

        var p = new Project
        {
            Name = req.Name.Trim(),
            Subject = req.Subject.Trim(),
            TeamId = req.TeamId,
            Deadline = ToUtc(req.Deadline)
        };

        _db.Projects.Add(p);
        await _db.SaveChangesAsync();
        return Ok(p);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Project>> Update(int id, [FromBody] UpdateProjectRequest req)
    {
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();

        var tasks = await _db.Tasks.AsNoTracking().Where(t => t.ProjectId == id).ToListAsync();

        var newDeadlineUtc = ToUtc(req.Deadline);

        try
        {
            TaskValidator.ValidateProjectDeadlineChange(p, newDeadlineUtc, tasks);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        p.Name = req.Name.Trim();
        p.Subject = req.Subject.Trim();
        p.Deadline = newDeadlineUtc;

        await _db.SaveChangesAsync();
        return Ok(p);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        _db.Projects.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
