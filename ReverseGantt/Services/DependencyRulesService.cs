using Microsoft.EntityFrameworkCore;
using ReverseGantt.Data;
using ReverseGantt.Models;

namespace ReverseGantt.Services;

public class DependencyRulesService
{
    private readonly AppDbContext _db;

    public DependencyRulesService(AppDbContext db)
    {
        _db = db;
    }

    public record Blocker(int PredecessorId, int SuccessorId, DependencyType Type, int TimeOffsetMinutes, string Message);

    DateTime GetStart(TaskItem t)
    {
        if (t.StartDate.HasValue) return t.StartDate.Value;
        var days = t.DurationDays.HasValue && t.DurationDays.Value > 0 ? t.DurationDays.Value : 1;
        return t.Deadline.AddDays(-days);
    }

    DateTime GetFinish(TaskItem t)
    {
        if (t.EndDate.HasValue) return t.EndDate.Value;
        return t.Deadline;
    }

    public async Task<List<Blocker>> GetStartBlockersAsync(int taskId, CancellationToken ct)
    {
        var succ = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (succ == null) return new List<Blocker>();

        var deps = await _db.Dependencies.AsNoTracking()
            .Where(d => d.SuccessorId == taskId)
            .ToListAsync(ct);

        if (deps.Count == 0) return new List<Blocker>();

        var predIds = deps.Select(d => d.PredecessorId).Distinct().ToList();

        var preds = await _db.Tasks.AsNoTracking()
            .Where(t => predIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var succStart = GetStart(succ);

        var blockers = new List<Blocker>();

        foreach (var d in deps)
        {
            if (!preds.TryGetValue(d.PredecessorId, out var pred)) continue;

            var lag = TimeSpan.FromMinutes(d.TimeOffsetMinutes);
            var predStart = GetStart(pred);
            var predFinish = GetFinish(pred);

            if (d.Type == DependencyType.FS)
            {
                if (pred.Status != Status.Done)
                {
                    blockers.Add(new Blocker(pred.Id, succ.Id, d.Type, d.TimeOffsetMinutes, $"FS: predecessor #{pred.Id} must be Done"));
                    continue;
                }

                if (predFinish + lag > succStart)
                    blockers.Add(new Blocker(pred.Id, succ.Id, d.Type, d.TimeOffsetMinutes, $"FS: predecessor #{pred.Id} finish+lag must be <= successor start"));
            }

            if (d.Type == DependencyType.SS)
            {
                if (predStart + lag > succStart)
                    blockers.Add(new Blocker(pred.Id, succ.Id, d.Type, d.TimeOffsetMinutes, $"SS: predecessor #{pred.Id} start+lag must be <= successor start"));
            }
        }

        return blockers;
    }

    public async Task<List<Blocker>> GetFinishBlockersAsync(int taskId, CancellationToken ct)
    {
        var succ = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (succ == null) return new List<Blocker>();

        var deps = await _db.Dependencies.AsNoTracking()
            .Where(d => d.SuccessorId == taskId)
            .ToListAsync(ct);

        if (deps.Count == 0) return new List<Blocker>();

        var predIds = deps.Select(d => d.PredecessorId).Distinct().ToList();

        var preds = await _db.Tasks.AsNoTracking()
            .Where(t => predIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var succFinish = GetFinish(succ);

        var blockers = new List<Blocker>();

        foreach (var d in deps)
        {
            if (!preds.TryGetValue(d.PredecessorId, out var pred)) continue;

            var lag = TimeSpan.FromMinutes(d.TimeOffsetMinutes);
            var predStart = GetStart(pred);
            var predFinish = GetFinish(pred);

            if (d.Type == DependencyType.FF)
            {
                if (predFinish + lag > succFinish)
                    blockers.Add(new Blocker(pred.Id, succ.Id, d.Type, d.TimeOffsetMinutes, $"FF: predecessor #{pred.Id} finish+lag must be <= successor finish"));
            }

            if (d.Type == DependencyType.SF)
            {
                if (predStart + lag > succFinish)
                    blockers.Add(new Blocker(pred.Id, succ.Id, d.Type, d.TimeOffsetMinutes, $"SF: predecessor #{pred.Id} start+lag must be <= successor finish"));
            }
        }

        return blockers;
    }

    public async Task<(bool Allowed, string? Reason)> CanMarkDoneAsync(int taskId, CancellationToken ct)
    {
        var startBlockers = await GetStartBlockersAsync(taskId, ct);
        if (startBlockers.Count > 0) return (false, startBlockers[0].Message);

        var finishBlockers = await GetFinishBlockersAsync(taskId, ct);
        if (finishBlockers.Count > 0) return (false, finishBlockers[0].Message);

        return (true, null);
    }
}
