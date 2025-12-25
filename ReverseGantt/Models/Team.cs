using System.ComponentModel.DataAnnotations;

namespace ReverseGantt.Models;

public class Team
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    public List<Participant> Participants { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
}
