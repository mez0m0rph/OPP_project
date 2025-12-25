using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Interfaces;
using ReverseGantt.Models;

namespace ReverseGantt.Services;

public class ExecutorService : IExecutorService
{
    private readonly AppDbContext _db;
    private readonly DependencyRulesService _rules;

    public ExecutorService(AppDbContext db, DependencyRulesService rules)
    {
        _db = db;
        _rules = rules;
    }

    public async Task ExecuteTaskAsync(int taskId, CancellationToken ct)
    {
        var (allowed, reason) = await _rules.CanMarkDoneAsync(taskId, ct);
        if (!allowed) throw new InvalidOperationException(reason ?? "Not allowed");

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null) throw new InvalidOperationException("Task not found");

        task.Status = Status.Done;
        await _db.SaveChangesAsync(ct);
    }
}
