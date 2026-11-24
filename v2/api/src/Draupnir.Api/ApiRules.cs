using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Draupnir.Api;

/// <summary>
/// Human Gate 的必要證據規則。前端的 app/lib/evidenceRules.ts 是同一套規則；
/// 後端必須自己再檢查一次，否則任何登入的管理者都能繞過 UI 直接核准。
/// </summary>
public static class GateRules
{
    public const string ExceptionPrefix = "【人工例外決定：缺少或未通過必要證據】";
    public const int MinimumExceptionReasonLength = 20;

    /// <summary>沒有通過的證據、有失敗的證據，或審查要求修改時，就不能直接核准。</summary>
    public static bool NeedsException(IEnumerable<EvidenceResult> results, IEnumerable<Verdict> verdicts)
    {
        var evidence = results as IReadOnlyCollection<EvidenceResult> ?? results.ToList();
        var reviews = verdicts as IReadOnlyCollection<Verdict> ?? verdicts.ToList();
        var hasPass = evidence.Contains(EvidenceResult.Pass);
        var hasFail = evidence.Contains(EvidenceResult.Fail);
        var changesRequested = reviews.Contains(Verdict.ChangesRequested);
        return !hasPass || hasFail || changesRequested;
    }

    /// <summary>人工例外必須帶固定前綴，且理由至少 20 字（去掉前後空白）。</summary>
    public static bool IsValidExceptionNote(string? note)
    {
        if (note is null || !note.StartsWith(ExceptionPrefix, StringComparison.Ordinal)) return false;
        return note[ExceptionPrefix.Length..].Trim().Length >= MinimumExceptionReasonLength;
    }

    /// <summary>沒有工作項目的 Gate 仍需一句可稽核的人工理由（至少 20 字）。</summary>
    public static bool IsValidManualReason(string? note) =>
        note?.Trim().Length >= MinimumExceptionReasonLength;

    /// <summary>
    /// The evidence set that the gate decision actually evaluates.
    /// Only the latest attempt of each source task (Source + SourceInstance + ExternalTaskId)
    /// is required; the gate's work item aggregates the per-task results. Evidence older than
    /// the work item's last update is stale and cannot be the sole basis for approval.
    /// Blocked and NotTested are not passes; they only affect the "missing pass" condition,
    /// while Fail and ChangesRequested block approval.
    /// </summary>
    public static GateEvidenceEvaluation Evaluate(WorkItem workItem,
        IEnumerable<AgentRun> runs, IEnumerable<Evidence> evidence, IEnumerable<Review> reviews)
    {
        var latestRuns = runs.Where(r => r.WorkItemId == workItem.Id)
            .GroupBy(r => new { r.Source, r.SourceInstance, r.ExternalTaskId })
            .Select(group => group.OrderByDescending(r => r.Attempt)
                .ThenByDescending(r => r.StartedAt).First())
            .ToList();
        var latestRunIds = latestRuns.Select(r => r.Id).ToHashSet();

        var latestEvidence = evidence.Where(e => e.WorkItemId == workItem.Id &&
            (e.AgentRunId is null || latestRunIds.Contains(e.AgentRunId.Value))).ToList();
        var ignoredEvidence = evidence.Where(e => e.WorkItemId == workItem.Id &&
            e.AgentRunId is not null && !latestRunIds.Contains(e.AgentRunId.Value)).ToList();
        var staleEvidence = latestEvidence.Where(e => e.RecordedAt < workItem.UpdatedAt).ToList();
        var usedEvidence = latestEvidence.Where(e => e.RecordedAt >= workItem.UpdatedAt).ToList();

        var latestReviews = reviews.Where(r => r.WorkItemId == workItem.Id &&
            (r.AgentRunId is null || latestRunIds.Contains(r.AgentRunId.Value))).ToList();
        var ignoredReviews = reviews.Where(r => r.WorkItemId == workItem.Id &&
            r.AgentRunId is not null && !latestRunIds.Contains(r.AgentRunId.Value)).ToList();

        return new GateEvidenceEvaluation(latestRuns, latestEvidence, usedEvidence, staleEvidence,
            ignoredEvidence, latestReviews, ignoredReviews);
    }

    public sealed class GateEvidenceEvaluation(
        IReadOnlyList<AgentRun> latestRuns,
        IReadOnlyList<Evidence> latestEvidence,
        IReadOnlyList<Evidence> usedEvidence,
        IReadOnlyList<Evidence> staleEvidence,
        IReadOnlyList<Evidence> ignoredEvidence,
        IReadOnlyList<Review> usedReviews,
        IReadOnlyList<Review> ignoredReviews)
    {
        public IReadOnlyList<AgentRun> LatestRuns { get; } = latestRuns;
        public IReadOnlyList<Evidence> LatestEvidence { get; } = latestEvidence;
        public IReadOnlyList<Evidence> UsedEvidence { get; } = usedEvidence;
        public IReadOnlyList<Evidence> StaleEvidence { get; } = staleEvidence;
        public IReadOnlyList<Evidence> IgnoredEvidence { get; } = ignoredEvidence;
        public IReadOnlyList<Review> UsedReviews { get; } = usedReviews;
        public IReadOnlyList<Review> IgnoredReviews { get; } = ignoredReviews;
        public bool HasPass => UsedEvidence.Any(e => e.Result == EvidenceResult.Pass);
        public bool HasFail => UsedEvidence.Any(e => e.Result == EvidenceResult.Fail);
        public bool ChangesRequested => UsedReviews.Any(r => r.Verdict == Verdict.ChangesRequested);
        public bool NeedsException => !HasPass || HasFail || ChangesRequested;
        public Dictionary<EvidenceResult, int> UsedCounts => Enum.GetValues<EvidenceResult>()
            .ToDictionary(result => result, result => UsedEvidence.Count(e => e.Result == result));
        public Dictionary<EvidenceResult, int> LatestCounts => Enum.GetValues<EvidenceResult>()
            .ToDictionary(result => result, result => LatestEvidence.Count(e => e.Result == result));
    }
}

public static class ApiRules
{
    public static IResult Missing() => Results.Problem("Resource not found.", statusCode: 404);
    public static IResult Conflict(string detail) => Results.Problem(detail, statusCode: 409);
    public static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    public static IResult? Validate(object model)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(model, new ValidationContext(model), results, true)) return null;
        return Results.ValidationProblem(results
            .SelectMany(result => result.MemberNames.DefaultIfEmpty("body")
                .Select(field => (Field: char.ToLowerInvariant(field[0]) + field[1..], result.ErrorMessage)))
            .GroupBy(x => x.Field)
            .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage ?? "Invalid value.").ToArray()));
    }

    public static bool IsDatabaseConflict(Exception error) => error switch
    {
        DbUpdateConcurrencyException => true,
        PostgresException pg => pg.SqlState is PostgresErrorCodes.UniqueViolation or
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.ForeignKeyViolation,
        DbUpdateException { InnerException: PostgresException pg } => IsDatabaseConflict(pg),
        _ => false
    };

    public static bool Reaches(Guid start, Guid target, IEnumerable<WorkItemDependency> edges)
    {
        var links = edges.ToLookup(edge => edge.WorkItemId, edge => edge.DependsOnId);
        var seen = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(start);
        while (pending.TryPop(out var id))
        {
            if (id == target) return true;
            if (!seen.Add(id)) continue;
            foreach (var next in links[id]) pending.Push(next);
        }
        return false;
    }

    public static async Task<IResult?> CheckStatus(DraupnirDb db, Guid id, WorkStatus status, string? reason)
    {
        if (status == WorkStatus.Blocked && string.IsNullOrWhiteSpace(reason))
            return Invalid("reason", "Blocked work requires a reason.");
        if (status == WorkStatus.Done && await db.Dependencies
            .Where(x => x.WorkItemId == id)
            .Join(db.WorkItems, edge => edge.DependsOnId, item => item.Id, (_, item) => item.Status)
            .AnyAsync(value => value != WorkStatus.Done))
            return Conflict("All dependencies must be Done.");
        if (status != WorkStatus.Done && await db.Dependencies
            .Where(x => x.DependsOnId == id)
            .Join(db.WorkItems, edge => edge.WorkItemId, item => item.Id, (_, item) => item.Status)
            .AnyAsync(value => value == WorkStatus.Done))
            return Conflict("Reopen completed dependents before reopening this work item.");
        return null;
    }
}
