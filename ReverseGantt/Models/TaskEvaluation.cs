using System.ComponentModel.DataAnnotations;

namespace ReverseGantt.Models;

public class TaskEvaluation
{
    public int Id { get; set; }

    [Required]
    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    [Required]
    public int TeacherId { get; set; }
    public Participant? Teacher { get; set; }

    public int? Score { get; set; }
    public string? Feedback { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
