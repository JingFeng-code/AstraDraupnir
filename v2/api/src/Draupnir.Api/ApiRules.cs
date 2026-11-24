using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Draupnir.Api;

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
