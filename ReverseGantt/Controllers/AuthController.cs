using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Models;
using ReverseGantt.Services;

namespace ReverseGantt.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly PasswordService _passwords;

    public AuthController(AppDbContext db, JwtTokenService jwt, PasswordService passwords)
    {
        _db = db;
        _jwt = jwt;
        _passwords = passwords;
    }

    public record RegisterRequest(string Name, string Email, string Password, int? TeamId, string? TeamName, RoleType Role);
    public record LoginRequest(string Email, string Password);

    public record AuthResponse(string Token, int UserId, int ParticipantId, string Role, string Email, string Name, int TeamId);

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Invalid input");

        var exists = await _db.Users.AnyAsync(u => u.Email == email);
        if (exists) return BadRequest("Email already exists");

        int teamId;

        if (req.TeamId.HasValue)
        {
            var ok = await _db.Teams.AnyAsync(t => t.Id == req.TeamId.Value);
            if (!ok) return BadRequest("Team not found");
            teamId = req.TeamId.Value;
        }
        else
        {
            var teamName = (req.TeamName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(teamName)) return BadRequest("Team is required");
            var team = new Team { Name = teamName };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync();
            teamId = team.Id;
        }

        var participant = new Participant
        {
            Name = req.Name.Trim(),
            Email = email,
            Role = req.Role,
            TeamId = teamId
        };

        _db.Participants.Add(participant);
        await _db.SaveChangesAsync();

        var user = new User
        {
            Email = email,
            PasswordHash = _passwords.Hash(req.Password),
            ParticipantId = participant.Id
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var loaded = await _db.Users.AsNoTracking()
            .Include(u => u.Participant)
            .FirstAsync(u => u.Id == user.Id);

        var token = _jwt.CreateToken(loaded);

        return Ok(new AuthResponse(
            token,
            loaded.Id,
            loaded.ParticipantId,
            loaded.Participant!.Role.ToString(),
            loaded.Email,
            loaded.Participant!.Name,
            loaded.Participant!.TeamId
        ));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.Participant)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null) return Unauthorized("Invalid credentials");
        if (!_passwords.Verify(req.Password, user.PasswordHash)) return Unauthorized("Invalid credentials");

        var token = _jwt.CreateToken(user);

        return Ok(new AuthResponse(
            token,
            user.Id,
            user.ParticipantId,
            user.Participant!.Role.ToString(),
            user.Email,
            user.Participant!.Name,
            user.Participant!.TeamId
        ));
    }
}
