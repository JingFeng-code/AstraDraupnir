using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Draupnir.Api.ApiRules;

namespace Draupnir.Api;

public static class DeliveryEndpoints
{
    public static void MapDeliveryEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/gates", async (Guid projectId, DraupnirDb db) =>
            await db.Projects.AnyAsync(p => p.Id == projectId)
                ? Results.Ok(await db.Gates.Where(x => x.ProjectId == projectId).OrderBy(x => x.Title).ToListAsync()) : Missing());
        api.MapPost("/projects/{projectId:guid}/gates", async (Guid projectId, GateInput input, DraupnirDb db) =>
        {
            if (string.IsNullOrWhiteSpace(input.Title)) return Invalid("title", "A title is required.");
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Missing();
            if (input.WorkItemId is { } id && !await db.WorkItems.AnyAsync(w => w.Id == id && w.ProjectId == projectId))
                return Invalid("workItemId", "Work item must belong to the project.");
            var gate = new HumanGate { Id = Guid.NewGuid(), Title = input.Title, ProjectId = projectId,
                WorkItemId = input.WorkItemId, ExternalRef = input.ExternalRef };
            db.Gates.Add(gate);
            await db.SaveChangesAsync();
            return Results.Created($"/api/projects/{projectId}/gates", gate);
        });
        api.MapPost("/gates/{id:guid}/decision", async (Guid id, GateDecision input, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (input.Approved is not { } approved) return Invalid("approved", "A decision is required.");
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                || !await db.Users.AnyAsync(x => x.Id == userId)) return Results.Problem(statusCode: 401);
            if (await db.Gates.FindAsync(id) is not { } gate) return Missing();
            if (gate.Status != GateStatus.Pending) return Conflict("This gate already has a human decision.");
            gate.Status = approved ? GateStatus.Approved : GateStatus.Rejected;
            gate.DecidedBy = userId;
            gate.DecidedAt = DateTimeOffset.UtcNow;
            gate.Note = input.Note;
            await db.SaveChangesAsync();
            return Results.Ok(gate);
        });

        api.MapGet("/agent-runs", async (Guid? projectId, Guid? workItemId, DraupnirDb db) =>
            await Runs(db, projectId, workItemId).OrderByDescending(x => x.FinishedAt ?? x.StartedAt)
                .ThenByDescending(x => x.Attempt).ThenBy(x => x.Id).ToListAsync());
        api.MapGet("/evidence", async (Guid? projectId, Guid? workItemId, DraupnirDb db) =>
            await db.Evidence.Where(e => (!workItemId.HasValue || e.WorkItemId == workItemId ||
                db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.WorkItemId == workItemId)) &&
                (!projectId.HasValue || e.ProjectId == projectId ||
                db.WorkItems.Any(w => w.Id == e.WorkItemId && w.ProjectId == projectId) ||
                db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.ProjectId == projectId)))
                .OrderByDescending(e => e.RecordedAt).ToListAsync());
        api.MapGet("/reviews", async (Guid? projectId, Guid? workItemId, DraupnirDb db) =>
            await db.Reviews.Where(e => (!workItemId.HasValue || e.WorkItemId == workItemId ||
                db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.WorkItemId == workItemId)) &&
                (!projectId.HasValue || db.WorkItems.Any(w => w.Id == e.WorkItemId && w.ProjectId == projectId) ||
                db.AgentRuns.Any(r => r.Id == e.AgentRunId && r.ProjectId == projectId)))
                .OrderByDescending(e => e.CreatedAt).ToListAsync());

        api.MapPost("/import/supervisor", SupervisorImport.Import);
        api.MapGet("/dashboard", async (Guid? projectId, DraupnirDb db) =>
        {
            if (projectId is { } id && !await db.Projects.AnyAsync(p => p.Id == id)) return Missing();
            return Results.Ok(await Dashboard(db, projectId));
        });
        api.MapGet("/projects/{projectId:guid}/reports", async (Guid projectId, DraupnirDb db) =>
            await db.Projects.AnyAsync(p => p.Id == projectId) ? Results.Ok(await db.Reports
                .Where(r => r.ProjectId == projectId).OrderByDescending(r => r.GeneratedAt)
                .Select(r => new { r.Id, r.ProjectId, r.Title, r.GeneratedAt, r.DocxPath }).ToListAsync()) : Missing());
        api.MapPost("/projects/{projectId:guid}/reports", CreateReport);
        api.MapGet("/reports/{id:guid}/markdown", async (Guid id, DraupnirDb db) =>
            await db.Reports.FindAsync(id) is { } report ? Results.Text(report.MarkdownBody, "text/markdown", Encoding.UTF8) : Missing());
        api.MapGet("/reports/{id:guid}/stylus.yaml", async (Guid id, DraupnirDb db) =>
            await db.Reports.FindAsync(id) is { } report ? Results.Text(report.StylusSpecYaml, "application/yaml", Encoding.UTF8) : Missing());
    }

    private static IQueryable<AgentRun> Runs(DraupnirDb db, Guid? projectId, Guid? workItemId) =>
        db.AgentRuns.Where(run => (!projectId.HasValue || run.ProjectId == projectId ||
            db.WorkItems.Any(w => w.Id == run.WorkItemId && w.ProjectId == projectId)) &&
            (!workItemId.HasValue || run.WorkItemId == workItemId));

    private static async Task<DashboardData> Dashboard(DraupnirDb db, Guid? projectId)
    {
        var items = db.WorkItems.Where(w => !projectId.HasValue || w.ProjectId == projectId);
        var grouped = await items.GroupBy(w => w.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        var statusCounts = Enum.GetValues<WorkStatus>().ToDictionary(x => x.ToString(),
            status => grouped.FirstOrDefault(g => g.Status == status)?.Count ?? 0);
        var active = db.Sprints.Where(s => s.Status == SprintStatus.Active && (!projectId.HasValue || s.ProjectId == projectId));
        var activeItems = items.Where(w => active.Any(s => s.Id == w.SprintId));
        var total = await activeItems.CountAsync();
        var done = await activeItems.CountAsync(w => w.Status == WorkStatus.Done);
        return new(statusCounts, total == 0 ? 0 : Math.Round(100m * done / total, 2), done, total,
            await items.Where(w => w.Status == WorkStatus.Blocked).OrderBy(w => w.Code).ToListAsync(),
            await db.Gates.Where(g => g.Status == GateStatus.Pending && (!projectId.HasValue || g.ProjectId == projectId))
                .OrderBy(g => g.Title).ToListAsync(),
            await Runs(db, projectId, null).OrderByDescending(r => r.FinishedAt ?? r.StartedAt ?? DateTimeOffset.MinValue)
                .ThenByDescending(r => r.Attempt).ThenBy(r => r.Id).Take(10).ToListAsync());
    }

    private static async Task<IResult> CreateReport(Guid projectId, ReportInput? input, DraupnirDb db,
        IConfiguration config, ILoggerFactory loggerFactory, CancellationToken cancellation)
    {
        if (await db.Projects.FindAsync(projectId) is not { } project) return Missing();
        var data = await Dashboard(db, projectId);
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
                || Runs(db, projectId, null).Any(r => r.Id == e.AgentRunId)).OrderByDescending(e => e.RecordedAt).Take(30)
                .Select(e => e.Title + " · " + (e.Path ?? e.Url ?? "")).ToListAsync())),
            ("Reviews", Lines((await db.Reviews.Where(e => db.WorkItems.Any(w => w.Id == e.WorkItemId && w.ProjectId == projectId)
                || Runs(db, projectId, null).Any(r => r.Id == e.AgentRunId)).OrderByDescending(e => e.CreatedAt).Take(30)
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
