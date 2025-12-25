using ReverseGantt.Models;

namespace ReverseGantt.Services;

public static class TaskValidator
{
    public static void ValidateForCreateOrUpdate(Project project, DateTime deadline, DateTime? start, DateTime? end, int? durationDays)
    {
        if (project.Deadline.HasValue && deadline > project.Deadline.Value)
            throw new InvalidOperationException("Task deadline cannot be later than project deadline");

        var hasStart = start.HasValue;
        var hasEnd = end.HasValue;

        if (hasStart != hasEnd)
            throw new InvalidOperationException("StartDate and EndDate must be both set or both null");

        if (hasStart && hasEnd)
        {
            if (start!.Value > deadline) throw new InvalidOperationException("StartDate cannot be after Deadline");
            if (end!.Value > deadline) throw new InvalidOperationException("EndDate cannot be after Deadline");
            if (end!.Value < start!.Value) throw new InvalidOperationException("EndDate cannot be earlier than StartDate");
        }
        else
        {
            if (!durationDays.HasValue || durationDays.Value < 1)
                throw new InvalidOperationException("DurationDays must be >= 1 when StartDate/EndDate are null");
        }
    }

    public static void ValidateProjectDeadlineChange(Project project, DateTime? newDeadline, IEnumerable<TaskItem> tasks)
    {
        if (!newDeadline.HasValue) return;
        var maxTaskDeadline = tasks.Select(t => t.Deadline).DefaultIfEmpty(DateTime.MinValue).Max();
        if (maxTaskDeadline > newDeadline.Value)
            throw new InvalidOperationException("Project deadline cannot be earlier than existing task deadlines");
    }

    public static void EnsureStatusNotReverted(Status current, Status next)
    {
        if (current == Status.Done && next != Status.Done)
            throw new InvalidOperationException("Cannot revert status from Done");
    }
}
