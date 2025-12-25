using System.ComponentModel.DataAnnotations;

namespace ReverseGantt.Models;

public class Participant
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    public string? Email { get; set; }

    public RoleType Role { get; set; } = RoleType.Participant;

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public List<TaskAssignment> Assignments { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
}
