using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ReverseGantt.Models;

namespace ReverseGantt.Services;

public class JwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _key;

    public JwtTokenService(string issuer, string audience, string key)
    {
        _issuer = issuer;
        _audience = audience;
        _key = key;
    }

    public string CreateToken(User user)
    {
        var role = user.Participant?.Role.ToString() ?? "Participant";
        var email = user.Email;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("participantId", user.ParticipantId.ToString()),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Email, email)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
