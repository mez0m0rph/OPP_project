using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Models;
using Microsoft.AspNetCore.Authorization;


namespace ReverseGantt.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TeamsController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateTeamRequest(string Name);

    [HttpGet]
    public async Task<ActionResult<List<Team>>> GetAll()
    {
        return await _db.Teams.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Team>> Create([FromBody] CreateTeamRequest req)
    {
        var team = new Team { Name = req.Name.Trim() };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync();
        return Ok(team);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
