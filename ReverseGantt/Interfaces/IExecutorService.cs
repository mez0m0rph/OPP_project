namespace ReverseGantt.Interfaces;

public interface IExecutorService
{
    Task ExecuteTaskAsync(int taskId, CancellationToken ct);
}
