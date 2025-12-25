using ReverseGantt.Models;

namespace ReverseGantt.Interfaces;

public interface IExecutor
{
    Task ExecuteAsync(TaskItem task, CancellationToken ct);
}
