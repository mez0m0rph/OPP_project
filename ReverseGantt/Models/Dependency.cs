using System.ComponentModel.DataAnnotations;

namespace ReverseGantt.Models;

public class Dependency
{
    public int Id { get; set; }

    [Required]
    public int PredecessorId { get; set; }
    public TaskItem? Predecessor { get; set; }

    [Required]
    public int SuccessorId { get; set; }
    public TaskItem? Successor { get; set; }

    public DependencyType Type { get; set; } = DependencyType.FS;

    public int TimeOffsetMinutes { get; set; } = 0;
}
