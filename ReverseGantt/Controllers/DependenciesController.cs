using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Models;

namespace ReverseGantt.Controllers;

[ApiController]
[Route("api/dependencies")]
public class DependenciesController : ControllerBase
{
    private readonly AppDbContext _db;

    public DependenciesController(AppDbContext db)
    {
        _db = db;
    }

    public record DependencyDto(int Id, int PredecessorId, int SuccessorId, DependencyType Type, int TimeOffsetMinutes);
    public record CreateDependencyRequest(int PredecessorId, int SuccessorId, DependencyType Type, int TimeOffsetMinutes);

    [HttpGet]
    public async Task<ActionResult<List<DependencyDto>>> GetByProject([FromQuery] int projectId)
    {
        var taskIds = await _db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId).Select(t => t.Id).ToListAsync();
        var deps = await _db.Dependencies.AsNoTracking()
            .Where(d => taskIds.Contains(d.PredecessorId) && taskIds.Contains(d.SuccessorId))
            .OrderBy(d => d.Id)
            .ToListAsync();

        return deps.Select(d => new DependencyDto(d.Id, d.PredecessorId, d.SuccessorId, d.Type, d.TimeOffsetMinutes)).ToList();
    }

    [HttpGet("task/{taskId:int}")]
    public async Task<ActionResult<List<DependencyDto>>> GetForSuccessor(int taskId)
    {
        var deps = await _db.Dependencies.AsNoTracking()
            .Where(d => d.SuccessorId == taskId)
            .OrderBy(d => d.Id)
            .ToListAsync();

        return deps.Select(d => new DependencyDto(d.Id, d.PredecessorId, d.SuccessorId, d.Type, d.TimeOffsetMinutes)).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<DependencyDto>> Create([FromBody] CreateDependencyRequest req)
    {
        if (req.PredecessorId == req.SuccessorId) return BadRequest("Self-dependency is not allowed");

        var pred = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.PredecessorId);
        var succ = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.SuccessorId);
        if (pred == null || succ == null) return BadRequest("Task not found");
        if (pred.ProjectId != succ.ProjectId) return BadRequest("Tasks must belong to the same project");

        var dep = new Dependency
        {
            PredecessorId = req.PredecessorId,
            SuccessorId = req.SuccessorId,
            Type = req.Type,
            TimeOffsetMinutes = req.TimeOffsetMinutes
        };

        _db.Dependencies.Add(dep);
        await _db.SaveChangesAsync();

        return Ok(new DependencyDto(dep.Id, dep.PredecessorId, dep.SuccessorId, dep.Type, dep.TimeOffsetMinutes));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var dep = await _db.Dependencies.FirstOrDefaultAsync(d => d.Id == id);
        if (dep == null) return NotFound();
        _db.Dependencies.Remove(dep);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
