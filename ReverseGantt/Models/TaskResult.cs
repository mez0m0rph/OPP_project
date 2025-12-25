using System.ComponentModel.DataAnnotations;

namespace ReverseGantt.Models;

public class TaskResult
{
    public int Id { get; set; }

    [Required]
    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    [Required]
    public int AuthorId { get; set; }
    public Participant? Author { get; set; }

    public string? ResultText { get; set; }
    public string? ResultUrl { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
