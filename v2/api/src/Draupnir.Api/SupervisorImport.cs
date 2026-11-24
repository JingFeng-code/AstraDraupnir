using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Draupnir.Api.ApiRules;

namespace Draupnir.Api;

public static class SupervisorImport
{
    public static async Task<IResult> Import(JsonElement payload, Guid? projectId, DraupnirDb db)
    {
        if (Get(payload, "tasks").ValueKind != JsonValueKind.Array)
            return Invalid("tasks", "Provide a Supervisor object with a tasks array.");
        var tasks = Get(payload, "tasks").EnumerateArray().ToList();
        if (tasks.Any(row => row.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(Text(row, "id"))
            || string.IsNullOrWhiteSpace(Text(row, "status"))))
            return Invalid("tasks", "Each task requires an id and status.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var projects = await db.Projects.ToListAsync();
        if (projectId is not null && !projects.Any(p => p.Id == projectId)) return Missing();
        var agents = Get(payload, "agents");
        var seenRuns = new HashSet<Guid>();
        var seenEvidence = new HashSet<Guid>();
        var seenReviews = new HashSet<Guid>();
        var seenGates = new HashSet<Guid>();
        foreach (var row in tasks)
        {
            var id = Text(row, "id")!.Trim();
            var status = Text(row, "status")!.Trim();
            var attempt = Attempt(row);
            var run = db.AgentRuns.Local.FirstOrDefault(r => r.Source == RunSource.Supervisor &&
                r.ExternalTaskId == id && r.Attempt == attempt)
                ?? await db.AgentRuns.SingleOrDefaultAsync(r => r.Source == RunSource.Supervisor &&
                    r.ExternalTaskId == id && r.Attempt == attempt);
            if (run is null)
            {
                run = new AgentRun { ExternalTaskId = id, Attempt = attempt, Source = RunSource.Supervisor };
                db.AgentRuns.Add(run);
            }

            var repo = Text(row, "repo")?.Trim();
            if (projectId is not null && run.ProjectId is { } existingProject && existingProject != projectId)
                return Conflict("A Supervisor run is already associated with another project.");
            var project = projects.SingleOrDefault(p => p.Id == (projectId ?? run.ProjectId));
            project ??= projects.FirstOrDefault(p => !string.IsNullOrWhiteSpace(repo) &&
                (string.Equals(p.Key, repo, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, repo, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.RepoPath, Text(row, "worktree"), StringComparison.OrdinalIgnoreCase) && p.RepoPath is not null));
            if (project is null)
            {
                var name = string.IsNullOrWhiteSpace(repo) ? "Supervisor" : repo;
                var key = ProjectKey(name);
                project = projects.SingleOrDefault(p => p.Key == key);
                if (project is null)
                {
                    project = new Project { Key = key, Name = name[..Math.Min(name.Length, 200)], RepoPath = Text(row, "worktree") };
                    db.Projects.Add(project);
                    projects.Add(project);
                }
            }
            if (Guid.TryParse(Text(row, "workItemId"), out var workItemId))
            {
                if (!await db.WorkItems.AnyAsync(item => item.Id == workItemId && item.ProjectId == project.Id))
                    return Invalid("workItemId", "An imported work item must belong to the selected project.");
                run.WorkItemId = workItemId;
            }
            run.ProjectId = project.Id;
            run.Agent = Text(row, "implementer") ?? Text(row, "agent") ?? "Unknown";
            run.Model = Text(row, "model") ?? Text(Get(agents, run.Agent), "model") ?? "";
            run.SessionId = Text(row, "sessionId");
            run.Status = status;
            run.StartedAt = Date(row, "startedAt") ?? run.StartedAt;
            run.FinishedAt = Date(row, "finishedAt") ?? run.FinishedAt;
            if (run.FinishedAt is null && Normalize(status) is "DONE" or "COMMITTED" or "ACCEPTED")
                run.FinishedAt = Date(row, "lastUpdated");
            seenRuns.Add(run.Id);

            var log = Text(row, "log") ?? Text(row, "verifyLog") ?? Text(Get(row, "verify"), "logPath");
            if (log is not null)
            {
                var verification = Get(row, "verify");
                var evidenceResult = Get(verification, "exitCode").TryInt(out var code)
                    ? (code == 0 ? EvidenceResult.Pass : EvidenceResult.Fail) : EvidenceResult.NotTested;
                seenEvidence.Add(await UpsertEvidence(db, run, "log", EvidenceKind.Log, "Supervisor log", log, null, evidenceResult, null));
            }
            var resultEvidence = Text(Get(row, "result"), "evidence") ?? Text(row, "evidence");
            if (resultEvidence is not null)
                seenEvidence.Add(await UpsertEvidence(db, run, "result", EvidenceKind.Document, "Agent result evidence", resultEvidence,
                    null, EvidenceResult.NotTested, null));
            foreach (var entry in Items(Get(row, "evidence")))
            {
                var path = Text(entry, "path");
                var url = Text(entry, "url");
                var title = Text(entry, "title") ?? "Supervisor evidence";
                var kind = ParseEnum(Text(entry, "kind"), EvidenceKind.Log);
                var result = ParseEnum(Text(entry, "result"), EvidenceResult.NotTested);
                var key = Fingerprint(Text(entry, "id") ?? string.Join("\0", kind, path, url, title));
                seenEvidence.Add(await UpsertEvidence(db, run, key, kind, title, path, url, result, Date(entry, "recordedAt")));
            }

            var review = Get(row, "review");
            var verdictText = Text(review, "verdict") ?? Text(row, "verdict");
            if (TryVerdict(verdictText, out var verdict))
            {
                const string key = "review";
                var entity = db.Reviews.Local.FirstOrDefault(r => r.AgentRunId == run.Id && r.ImportKey == key)
                    ?? await db.Reviews.SingleOrDefaultAsync(r => r.AgentRunId == run.Id && r.ImportKey == key);
                if (entity is null)
                {
                    entity = new Review { AgentRunId = run.Id, ImportKey = key, CreatedAt = Date(review, "createdAt") ?? DateTimeOffset.UtcNow };
                    db.Reviews.Add(entity);
                }
                entity.WorkItemId = run.WorkItemId;
                entity.Verdict = verdict;
                entity.Reviewer = Text(review, "reviewer") ?? Text(row, "reviewer") ?? "Unknown";
                var findings = Get(review, "findings");
                if (findings.ValueKind == JsonValueKind.Undefined) findings = Get(row, "findings");
                entity.Findings = findings.ValueKind == JsonValueKind.Undefined ? JsonSerializer.SerializeToElement(Array.Empty<object>()) : findings.Clone();
                seenReviews.Add(entity.Id);
            }

            if (Normalize(status) == "WAITINGHUMAN" || Get(row, "requiresHuman").ValueKind == JsonValueKind.True)
            {
                var gateInput = Get(row, "gate");
                var externalRef = Text(gateInput, "externalRef") ?? $"supervisor:{id}";
                var gate = db.Gates.Local.FirstOrDefault(g => g.ProjectId == project.Id && g.ExternalRef == externalRef)
                    ?? await db.Gates.SingleOrDefaultAsync(g => g.ProjectId == project.Id && g.ExternalRef == externalRef);
                if (gate is null)
                {
                    gate = new HumanGate { Id = Guid.NewGuid(), ProjectId = project.Id, ExternalRef = externalRef, Status = GateStatus.Pending };
                    db.Gates.Add(gate);
                }
                if (gate.Status == GateStatus.Pending)
                {
                    gate.WorkItemId = run.WorkItemId;
                    gate.Title = Text(gateInput, "title") ?? Text(row, "title") ?? id;
                }
                seenGates.Add(gate.Id);
            }
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { agentRuns = seenRuns.Count, evidence = seenEvidence.Count, reviews = seenReviews.Count, gates = seenGates.Count });
    }

    private static async Task<Guid> UpsertEvidence(DraupnirDb db, AgentRun run, string key, EvidenceKind kind, string title,
        string? path, string? url, EvidenceResult result, DateTimeOffset? date)
    {
        var entity = db.Evidence.Local.FirstOrDefault(e => e.AgentRunId == run.Id && e.ImportKey == key)
            ?? await db.Evidence.SingleOrDefaultAsync(e => e.AgentRunId == run.Id && e.ImportKey == key);
        if (entity is null)
        {
            entity = new Evidence { AgentRunId = run.Id, ImportKey = key };
            db.Evidence.Add(entity);
        }
        entity.WorkItemId = run.WorkItemId;
        entity.Kind = kind;
        entity.Title = title;
        entity.Path = path;
        entity.Url = url;
        entity.Result = result;
        entity.RecordedAt = date ?? entity.RecordedAt;
        return entity.Id;
    }

    public static int Attempt(JsonElement row) =>
        Get(row, "attempt").TryInt(out var number) || Get(row, "attempts").TryInt(out number) ? Math.Max(1, number) : 1;
    public static string Fingerprint(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    public static string ProjectKey(string name) => "IM" + new string(Fingerprint(name.Trim().ToUpperInvariant()).Take(8)
        .Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))).ToArray());
    public static bool TryVerdict(string? value, out Verdict verdict)
    {
        verdict = Normalize(value) == "CHANGESREQUESTED" ? Verdict.ChangesRequested : Verdict.Pass;
        return Normalize(value) is "PASS" or "CHANGESREQUESTED";
    }

    private static string Normalize(string? text) => string.Concat((text ?? "").Where(char.IsLetterOrDigit)).ToUpperInvariant();
    private static T ParseEnum<T>(string? text, T fallback) where T : struct, Enum =>
        Enum.GetValues<T>().FirstOrDefault(item => Normalize(item.ToString()) == Normalize(text), fallback);
    private static JsonElement Get(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var found) ? found : default;
    private static string? Text(JsonElement value, string key) =>
        Get(value, key) is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;
    private static IEnumerable<JsonElement> Items(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    private static DateTimeOffset? Date(JsonElement value, string key) =>
        DateTimeOffset.TryParse(Text(value, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date.ToUniversalTime() : null;
    private static bool TryInt(this JsonElement value, out int number)
    {
        number = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number);
    }
}
