using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using static Draupnir.Api.ApiRules;

namespace Draupnir.Api;

public static class DeliveryEndpoints
{
    public static void MapDeliveryEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/gates", async (Guid projectId, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Missing();
            if (!await Access.CanProject(db, user, projectId)) return Access.Forbidden();
            return Results.Ok(await db.Gates.Where(x => x.ProjectId == projectId).OrderBy(x => x.Title).ToListAsync());
        });
        api.MapPost("/projects/{projectId:guid}/gates", async (Guid projectId, GateInput input, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (string.IsNullOrWhiteSpace(input.Title)) return Invalid("title", "A title is required.");
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Missing();
            if (!await Access.CanProject(db, user, projectId)) return Access.Forbidden();
            if (input.WorkItemId is { } id && !await db.WorkItems.AnyAsync(w => w.Id == id && w.ProjectId == projectId))
                return Invalid("workItemId", "Work item must belong to the project.");
            var gate = new HumanGate { Id = Guid.NewGuid(), Title = input.Title, ProjectId = projectId,
                WorkItemId = input.WorkItemId, ExternalRef = input.ExternalRef };
            db.Gates.Add(gate);
            await Access.Audit(db, user.Id, "gate.create", "gate", gate.Id, $"project={projectId}");
            await db.SaveChangesAsync();
            return Results.Created($"/api/projects/{projectId}/gates", gate);
        });
        api.MapPost("/gates/{id:guid}/decision", async (Guid id, GateDecision input, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (!Access.IsAdmin(user)) return Access.Forbidden();
            if (input.Approved is not { } approved) return Invalid("approved", "A decision is required.");
            // 讀取證據、評估規則、寫入決策必須在同一個交易裡：否則決策可能記到一份已經被更新過的證據。
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.Gates.FindAsync(id) is not { } gate) return Missing();
            if (gate.Status != GateStatus.Pending) return Conflict("This gate already has a human decision.");
            GateRules.GateEvidenceEvaluation? evaluation = null;
            WorkItem? workItem = null;
            var runById = new Dictionary<Guid, AgentRun>();
            if (gate.WorkItemId is { } workItemId)
            {
                workItem = await db.WorkItems.FindAsync(workItemId);
                if (workItem is null) return Missing();
                var runs = await db.AgentRuns.Where(r => r.WorkItemId == workItemId).ToListAsync();
                runById = runs.ToDictionary(r => r.Id);
                var evidence = await db.Evidence.Where(e => e.WorkItemId == workItemId).ToListAsync();
                var reviews = await db.Reviews.Where(r => r.WorkItemId == workItemId).ToListAsync();
                evaluation = GateRules.Evaluate(workItem, runs, evidence, reviews);
                // 前端 app/lib/evidenceRules.ts 的規則必須在後端再驗一次，
                // 否則直接呼叫這個端點就能跳過必要證據。
                if (approved && evaluation.NeedsException && !GateRules.IsValidExceptionNote(input.Note))
                    return Invalid("note", "必要證據未齊全：核准必須使用人工例外，並寫明至少 20 字的理由。");
            }
            if (approved && gate.WorkItemId is null && !GateRules.IsValidManualReason(input.Note))
                return Invalid("note", "沒有對應工作項目的 Gate 仍需寫明至少 20 字的人工判斷理由。");

            gate.Status = approved ? GateStatus.Approved : GateStatus.Rejected;
            gate.DecidedBy = user.Id;
            gate.DecidedAt = DateTimeOffset.UtcNow;
            gate.Note = input.Note;
            gate.EvidenceSnapshot = JsonSerializer.SerializeToElement(new
            {
                evaluatedAt = gate.DecidedAt,
                workItemId = gate.WorkItemId,
                workItemUpdatedAt = workItem?.UpdatedAt,
                rule = "latest attempt per Source+SourceInstance; fresh evidence only; Pass required; Fail/ChangesRequested block; Blocked/NotTested are not passes",
                latestAttempts = evaluation?.LatestRuns.Select(r => new
                {
                    runId = r.Id, source = r.Source.ToString(), sourceInstance = r.SourceInstance, attempt = r.Attempt
                }),
                counts = (evaluation?.UsedCounts ?? new Dictionary<EvidenceResult, int>())
                    .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                latestCounts = (evaluation?.LatestCounts ?? new Dictionary<EvidenceResult, int>())
                    .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                staleCount = evaluation?.StaleEvidence.Count ?? 0,
                usedEvidenceIds = evaluation?.UsedEvidence.Select(e => e.Id),
                // 決策當下實際使用的證據內容：即使之後重新匯入改寫原證據，歷史決策仍可追溯。
                usedEvidence = evaluation?.UsedEvidence.Select(e => new
                {
                    id = e.Id,
                    kind = e.Kind.ToString(),
                    result = e.Result.ToString(),
                    recordedAt = e.RecordedAt,
                    title = e.Title,
                    path = e.Path,
                    url = e.Url,
                    source = e.AgentRunId is { } runId && runById.TryGetValue(runId, out var run) ? run.Source.ToString() : null,
                    sourceInstance = e.AgentRunId is { } runId2 && runById.TryGetValue(runId2, out var run2) ? run2.SourceInstance : null,
                    externalTaskId = e.AgentRunId is { } runId3 && runById.TryGetValue(runId3, out var run3) ? run3.ExternalTaskId : null,
                    attempt = e.AgentRunId is { } runId4 && runById.TryGetValue(runId4, out var run4) ? run4.Attempt : (int?)null,
                    contentHash = EvidenceHash(e)
                }),
                usedReviews = evaluation?.UsedReviews.Select(r => new
                {
                    id = r.Id,
                    reviewer = r.Reviewer,
                    verdict = r.Verdict.ToString(),
                    createdAt = r.CreatedAt
                }),
                usedReviewIds = evaluation?.UsedReviews.Select(r => r.Id),
                staleEvidenceIds = evaluation?.StaleEvidence.Select(e => e.Id),
                ignoredEvidenceIds = evaluation?.IgnoredEvidence.Select(e => e.Id),
                ignoredReviewIds = evaluation?.IgnoredReviews.Select(r => r.Id),
                exceptionUsed = approved && evaluation?.NeedsException == true,
                manualReason = gate.WorkItemId is null ? input.Note : null
            });
            var auditDetail = $"{gate.Status}; project={gate.ProjectId}";
            if (gate.WorkItemId is null && approved) auditDetail += $"; reason={input.Note!.Trim()}";
            await Access.Audit(db, user.Id, "gate.decision", "gate", gate.Id, auditDetail);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(gate);
        });

        api.MapGet("/agent-runs", async (Guid? projectId, Guid? workItemId, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (projectId is { } selected && !await Access.CanProject(db, user, selected)) return Access.Forbidden();
            if (workItemId is { } itemId)
            {
                if (await db.WorkItems.FindAsync(itemId) is not { } item) return Missing();
                if (!await Access.CanProject(db, user, item.ProjectId)) return Access.Forbidden();
            }
            var visible = await Access.VisibleProjectIds(db, user);
            return Results.Ok(await Runs(db, projectId, workItemId, Access.IsAdmin(user), visible)
                .OrderByDescending(x => x.FinishedAt ?? x.StartedAt)
                .ThenByDescending(x => x.Attempt).ThenBy(x => x.Id).ToListAsync());
        });
        api.MapGet("/evidence", async (Guid? projectId, Guid? workItemId, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (projectId is { } selected && !await Access.CanProject(db, user, selected)) return Access.Forbidden();
            if (workItemId is { } itemId)
            {
                if (await db.WorkItems.FindAsync(itemId) is not { } item) return Missing();
                if (!await Access.CanProject(db, user, item.ProjectId)) return Access.Forbidden();
            }
            var visible = await Access.VisibleProjectIds(db, user);
            return Results.Ok(await Evidence(db, projectId, workItemId, Access.IsAdmin(user), visible)
                .OrderByDescending(e => e.RecordedAt).ToListAsync());
        });
        api.MapGet("/reviews", async (Guid? projectId, Guid? workItemId, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (projectId is { } selected && !await Access.CanProject(db, user, selected)) return Access.Forbidden();
            if (workItemId is { } itemId)
            {
                if (await db.WorkItems.FindAsync(itemId) is not { } item) return Missing();
                if (!await Access.CanProject(db, user, item.ProjectId)) return Access.Forbidden();
            }
            var visible = await Access.VisibleProjectIds(db, user);
            return Results.Ok(await Reviews(db, projectId, workItemId, Access.IsAdmin(user), visible)
                .OrderByDescending(e => e.CreatedAt).ToListAsync());
        });

        api.MapPost("/import/supervisor", async (JsonElement payload, Guid? projectId, string? sourceInstance, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (!Access.IsAdmin(user)) return Access.Forbidden();
            if (projectId is { } selected && !await Access.CanProject(db, user, selected)) return Access.Forbidden();
            return await SupervisorImport.Import(payload, projectId, db, user.Id, sourceInstance);
        });

        api.MapGet("/dashboard", async (Guid? projectId, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (projectId is { } selected && !await Access.CanProject(db, user, selected)) return Access.Forbidden();
            var visible = await Access.VisibleProjectIds(db, user);
            return Results.Ok(await Dashboard(db, projectId, Access.IsAdmin(user), visible));
        });

        api.MapGet("/projects/{projectId:guid}/reports", async (Guid projectId, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Missing();
            if (!await Access.CanProject(db, user, projectId)) return Access.Forbidden();
            return Results.Ok(await db.Reports.Where(r => r.ProjectId == projectId).OrderByDescending(r => r.GeneratedAt)
                .Select(r => new { r.Id, r.ProjectId, r.Title, r.GeneratedAt, r.DocxPath }).ToListAsync());
        });
        api.MapPost("/projects/{projectId:guid}/reports", CreateReport);
        api.MapGet("/reports/{id:guid}/markdown", async (Guid id, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (await db.Reports.FindAsync(id) is not { } report) return Missing();
            if (!await Access.CanProject(db, user, report.ProjectId)) return Access.Forbidden();
            return Results.Text(report.MarkdownBody, "text/markdown", Encoding.UTF8);
        });
        api.MapGet("/reports/{id:guid}/stylus.yaml", async (Guid id, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (await db.Reports.FindAsync(id) is not { } report) return Missing();
            if (!await Access.CanProject(db, user, report.ProjectId)) return Access.Forbidden();
            return Results.Text(report.StylusSpecYaml, "application/yaml", Encoding.UTF8);
        });
    }

    /// <summary>證據內容的穩定雜湊：之後重新匯入改寫原證據時，快照仍能指出當時的內容。</summary>
    private static string EvidenceHash(Evidence evidence)
    {
        var payload = !string.IsNullOrWhiteSpace(evidence.Data)
            ? evidence.Data!
            : $"{evidence.Kind}|{evidence.Title}|{evidence.Path}|{evidence.Url}|{evidence.Result}|{evidence.RecordedAt:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static IQueryable<AgentRun> Runs(DraupnirDb db, Guid? projectId, Guid? workItemId, bool admin, List<Guid> visible) =>
        db.AgentRuns.Where(run => (!projectId.HasValue || run.ProjectId == projectId) &&
            (!workItemId.HasValue || run.WorkItemId == workItemId) &&
            (admin || (run.ProjectId.HasValue && visible.Contains(run.ProjectId.Value)) ||
             db.WorkItems.Any(w => w.Id == run.WorkItemId && visible.Contains(w.ProjectId))));

    private static IQueryable<Evidence> Evidence(DraupnirDb db, Guid? projectId, Guid? workItemId, bool admin, List<Guid> visible) =>
        db.Evidence.Where(e => (!workItemId.HasValue || e.WorkItemId == workItemId ||
            db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.WorkItemId == workItemId)) &&
            (!projectId.HasValue || e.ProjectId == projectId ||
            db.WorkItems.Any(w => w.Id == e.WorkItemId && w.ProjectId == projectId) ||
            db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.ProjectId == projectId)) &&
            (admin || (e.ProjectId.HasValue && visible.Contains(e.ProjectId.Value)) ||
             db.WorkItems.Any(w => w.Id == e.WorkItemId && visible.Contains(w.ProjectId)) ||
             db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.ProjectId.HasValue && visible.Contains(r.ProjectId.Value))));

    private static IQueryable<Review> Reviews(DraupnirDb db, Guid? projectId, Guid? workItemId, bool admin, List<Guid> visible) =>
        db.Reviews.Where(e => (!workItemId.HasValue || e.WorkItemId == workItemId ||
            db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.WorkItemId == workItemId)) &&
            (!projectId.HasValue || db.WorkItems.Any(w => w.Id == e.WorkItemId && w.ProjectId == projectId) ||
            db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.ProjectId == projectId)) &&
            (admin || db.WorkItems.Any(w => w.Id == e.WorkItemId && visible.Contains(w.ProjectId)) ||
             db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.ProjectId.HasValue && visible.Contains(r.ProjectId.Value))));

    private static async Task<DashboardData> Dashboard(DraupnirDb db, Guid? projectId, bool admin, List<Guid> visible)
    {
        var items = db.WorkItems.Where(w => projectId.HasValue ? w.ProjectId == projectId : admin || visible.Contains(w.ProjectId));
        var grouped = await items.GroupBy(w => w.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        var statusCounts = Enum.GetValues<WorkStatus>().ToDictionary(x => x.ToString(),
            status => grouped.FirstOrDefault(g => g.Status == status)?.Count ?? 0);
        var active = db.Sprints.Where(s => s.Status == SprintStatus.Active &&
            (projectId.HasValue ? s.ProjectId == projectId : admin || visible.Contains(s.ProjectId)));
        var activeItems = items.Where(w => active.Any(s => s.Id == w.SprintId));
        var total = await activeItems.CountAsync();
        var done = await activeItems.CountAsync(w => w.Status == WorkStatus.Done);
        return new(statusCounts, total == 0 ? 0 : Math.Round(100m * done / total, 2), done, total,
            await items.Where(w => w.Status == WorkStatus.Blocked).OrderBy(w => w.Code).ToListAsync(),
            await db.Gates.Where(g => g.Status == GateStatus.Pending &&
                (projectId.HasValue ? g.ProjectId == projectId : admin || visible.Contains(g.ProjectId)))
                .OrderBy(g => g.Title).ToListAsync(),
            await Runs(db, projectId, null, admin, visible).OrderByDescending(r => r.FinishedAt ?? r.StartedAt ?? DateTimeOffset.MinValue)
                .ThenByDescending(r => r.Attempt).ThenBy(r => r.Id).Take(10).ToListAsync());
    }

    private static async Task<IResult> CreateReport(Guid projectId, ReportInput? input, ClaimsPrincipal principal, DraupnirDb db,
        IConfiguration config, ILoggerFactory loggerFactory, CancellationToken cancellation)
    {
        if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
        if (!await Access.CanProject(db, user, projectId)) return Access.Forbidden();
        if (await db.Projects.FindAsync(projectId) is not { } project) return Missing();
        var visible = await Access.VisibleProjectIds(db, user);
        var data = await Dashboard(db, projectId, Access.IsAdmin(user), visible);
        var title = string.IsNullOrWhiteSpace(input?.Title) ? $"{project.Name} delivery report" : input.Title.Trim();
        var summary = string.Join(", ", data.StatusCounts.Select(x => $"{x.Key}: {x.Value}"));
        var sections = new (string Heading, string Text)[]
        {
            ("Progress", $"{summary}\nActive sprint: {data.ActiveSprintDone}/{data.ActiveSprintTotal} ({data.ActiveSprintCompletionRate}%)"),
            ("Blocked work", Lines(data.BlockedItems.Select(x => $"{x.Code} {x.Title}: {x.BlockedReason}"))),
            ("Pending human gates", Lines(data.PendingGates.Select(x => x.Title))),
            ("Recent agent runs", Lines(data.RecentAgentRuns.Select(x => $"{x.ExternalTaskId} · {x.Agent} · {x.Status}"))),
            ("Evidence", Lines(await db.Evidence.Where(e => e.ProjectId == projectId
                || db.WorkItems.Any(w => w.Id == e.WorkItemId && w.ProjectId == projectId)
                || Runs(db, projectId, null, Access.IsAdmin(user), visible).Any(r => r.Id == e.AgentRunId)).OrderByDescending(e => e.RecordedAt).Take(30)
                .Select(e => e.Title + " · " + (e.Path ?? e.Url ?? "")).ToListAsync())),
            ("Reviews", Lines((await db.Reviews.Where(e => db.WorkItems.Any(w => w.Id == e.WorkItemId && w.ProjectId == projectId)
                || Runs(db, projectId, null, Access.IsAdmin(user), visible).Any(r => r.Id == e.AgentRunId)).OrderByDescending(e => e.CreatedAt).Take(30)
                .ToListAsync()).Select(r => $"{r.Reviewer} · {r.Verdict}")))
        };
        var blocks = new List<object> { new { type = "toc", title = "Contents", levels = 2 } };
        foreach (var section in sections)
        {
            blocks.Add(new { type = "heading", level = 1, text = section.Heading });
            blocks.Add(new { type = "text", text = section.Text });
        }
        var report = new DeliveryReport
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = title,
            MarkdownBody = $"# {title}\n\n" + string.Join("\n\n", sections.Select(s => $"## {s.Heading}\n\n{s.Text}")) + "\n",
            StylusSpecYaml = JsonSerializer.Serialize(new { spec_version = 1, title, page = new { size = "A4", show_page_numbers = true }, blocks },
                new JsonSerializerOptions { WriteIndented = true })
        };
        await RenderDocx(report, config, loggerFactory.CreateLogger("Draupnir.Reports"), cancellation);
        db.Reports.Add(report);
        await Access.Audit(db, user.Id, "report.generate", "report", report.Id, $"project={projectId}");
        await db.SaveChangesAsync(cancellation);
        return Results.Created($"/api/reports/{report.Id}/markdown", report);
    }

    private static string Lines(IEnumerable<string> entries)
    {
        var text = string.Join("\n", entries.Select(value => "- " + value));
        return text.Length == 0 ? "None." : text;
    }

    private static async Task RenderDocx(DeliveryReport report, IConfiguration config, ILogger logger, CancellationToken cancellation)
    {
        var executable = config["DRAUPNIR_STYLUS_EXECUTABLE"] ?? FindStylusOnPath();
        if (string.IsNullOrWhiteSpace(executable)) return;
        var directory = Path.GetFullPath(config["DRAUPNIR_REPORT_DIR"] ?? "reports");
        try
        {
            Directory.CreateDirectory(directory);
            var spec = Path.Combine(directory, $"{report.Id}.yaml");
            var docx = Path.Combine(directory, $"{report.Id}.docx");
            await File.WriteAllTextAsync(spec, report.StylusSpecYaml, cancellation);
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = directory, CreateNoWindow = true };
            foreach (var arg in new[] { "render", spec, "--format", "docx", "-o", docx }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info);
            if (process is null) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                    try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                cancellation.ThrowIfCancellationRequested();
                logger.LogWarning("Stylus rendering timed out for {ReportId}.", report.Id);
                return;
            }
            if (process.ExitCode == 0 && File.Exists(docx)) report.DocxPath = docx;
            else logger.LogWarning("Stylus exited with code {ExitCode} for {ReportId}.", process.ExitCode, report.Id);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(error, "DOCX rendering unavailable for {ReportId}.", report.Id);
        }
    }

    private static string? FindStylusOnPath()
    {
        var name = OperatingSystem.IsWindows() ? "astra-stylus.exe" : "astra-stylus";
        return (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(directory => Path.IsPathFullyQualified(directory))
            .Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists);
    }

    public sealed record GateInput(string? Title, Guid? WorkItemId, string? ExternalRef);
    public sealed record GateDecision(bool? Approved, string? Note);
    public sealed record ReportInput(string? Title);
    public sealed record DashboardData(Dictionary<string, int> StatusCounts, decimal ActiveSprintCompletionRate,
        int ActiveSprintDone, int ActiveSprintTotal, List<WorkItem> BlockedItems, List<HumanGate> PendingGates,
        List<AgentRun> RecentAgentRuns);
}
