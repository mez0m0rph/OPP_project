using System.ComponentModel.DataAnnotations;

namespace ReverseGantt.Models;

public class Comment
{
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int? TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    [Required]
    public int AuthorId { get; set; }
    public Participant? Author { get; set; }

    [Required]
    public string Text { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
