using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Models;

namespace ReverseGantt.Controllers;

[ApiController]
[Route("api/participants")]
public class ParticipantsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ParticipantsController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateParticipantRequest(string Name, string? Email, RoleType Role, int TeamId);

    [HttpGet]
    public async Task<ActionResult<List<Participant>>> Get([FromQuery] int teamId)
    {
        return await _db.Participants.AsNoTracking()
            .Where(p => p.TeamId == teamId)
            .OrderBy(p => p.Id)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Participant>> Create([FromBody] CreateParticipantRequest req)
    {
        var exists = await _db.Teams.AnyAsync(t => t.Id == req.TeamId);
        if (!exists) return BadRequest("Team not found");

        var p = new Participant
        {
            Name = req.Name.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Role = req.Role,
            TeamId = req.TeamId
        };

        _db.Participants.Add(p);
        await _db.SaveChangesAsync();
        return Ok(p);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Participants.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        _db.Participants.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
