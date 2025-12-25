using System.ComponentModel.DataAnnotations;

namespace ReverseGantt.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    public string Email { get; set; } = "";

    [Required]
    public string PasswordHash { get; set; } = "";

    public int ParticipantId { get; set; }
    public Participant? Participant { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
