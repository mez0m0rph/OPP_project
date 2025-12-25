using System.ComponentModel.DataAnnotations;

namespace ReverseGantt.Models;

public class Project
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    [Required]
    public string Subject { get; set; } = "";

    public DateTime? Deadline { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public List<TaskItem> Tasks { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
}
