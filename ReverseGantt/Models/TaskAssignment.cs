namespace ReverseGantt.Models;

public class TaskAssignment
{
    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public int ParticipantId { get; set; }
    public Participant? Participant { get; set; }
}
