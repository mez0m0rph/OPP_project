using System.ComponentModel.DataAnnotations;

namespace ReverseGantt.Models;

public class TaskItem
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    [Required]
    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [Required]
    public DateTime Deadline { get; set; }

    public int? DurationDays { get; set; }

    public Status Status { get; set; } = Status.InProgress;

    public List<TaskAssignment> Assignments { get; set; } = new();

    public List<Dependency> PredecessorDependencies { get; set; } = new();
    public List<Dependency> SuccessorDependencies { get; set; } = new();
}
