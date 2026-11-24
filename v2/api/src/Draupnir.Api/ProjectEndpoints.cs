using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using static Draupnir.Api.ApiRules;

namespace Draupnir.Api;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/projects", async (DraupnirDb db) => await db.Projects.OrderBy(x => x.Key).ToListAsync());
        api.MapGet("/projects/{id:guid}", async (Guid id, DraupnirDb db) =>
            await db.Projects.FindAsync(id) is { } project ? Results.Ok(project) : Missing());
        api.MapPost("/projects", async (Project input, DraupnirDb db) =>
        {
            if (Validate(input) is { } invalid) return invalid;
            input.Id = Guid.NewGuid();
            input.CreatedAt = input.UpdatedAt = DateTimeOffset.UtcNow;
            db.Projects.Add(input);
            await db.SaveChangesAsync();
            return Results.Created($"/api/projects/{input.Id}", input);
        });
        api.MapPut("/projects/{id:guid}", async (Guid id, Project input, DraupnirDb db) =>
        {
            if (Validate(input) is { } invalid) return invalid;
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.Projects.FindAsync(id) is not { } project) return Missing();
            if (project.Key != input.Key && await db.WorkItems.AnyAsync(x => x.ProjectId == id))
                return Conflict("The project key cannot change after work item codes exist.");
            input.Id = id;
            input.CreatedAt = project.CreatedAt;
            input.UpdatedAt = DateTimeOffset.UtcNow;
            db.Entry(project).CurrentValues.SetValues(input);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(project);
        });
        api.MapDelete("/projects/{id:guid}", async (Guid id, DraupnirDb db) =>
        {
            if (await db.Projects.FindAsync(id) is not { } project) return Missing();
            if (project.Status != ProjectStatus.Archived)
            {
                project.Status = ProjectStatus.Archived;
                project.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }
            return Results.NoContent();
        });

        MapChildren<Requirement>(api, "requirements");
        MapChildren<WorkItem>(api, "work-items", list: false);
        MapChildren<Sprint>(api, "sprints");
        MapChildren<Milestone>(api, "milestones");
        api.MapGet("/projects/{projectId:guid}/work-items", ListWorkItems);
        api.MapGet("/work-items/{id:guid}/dependencies", async (Guid id, DraupnirDb db) =>
            await db.WorkItems.AnyAsync(x => x.Id == id)
                ? Results.Ok(await db.Dependencies.Where(x => x.WorkItemId == id).ToListAsync()) : Missing());
        api.MapPost("/work-items/{id:guid}/dependencies", AddDependency);
        api.MapDelete("/work-items/{id:guid}/dependencies/{depId:guid}", async (Guid id, Guid depId, DraupnirDb db) =>
        {
            if (await db.Dependencies.FindAsync(id, depId) is not { } dependency) return Missing();
            db.Dependencies.Remove(dependency);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        api.MapPatch("/work-items/{id:guid}/status", async (Guid id, StatusChange input, DraupnirDb db) =>
        {
            if (input.Status is not { } status) return Invalid("status", "A status is required.");
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.WorkItems.FindAsync(id) is not { } item) return Missing();
            if (await CheckStatus(db, id, status, input.Reason) is { } issue) return issue;
            item.Status = status;
            item.BlockedReason = status == WorkStatus.Blocked ? input.Reason : null;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.Ok(item);
        });
    }

    private static void MapChildren<T>(RouteGroupBuilder api, string resource, bool list = true)
        where T : class, IProjectChild
    {
        var route = "/projects/{projectId:guid}/" + resource;
        if (list)
            api.MapGet(route, async (Guid projectId, DraupnirDb db) =>
                await db.Projects.AnyAsync(x => x.Id == projectId)
                    ? Results.Ok(await db.Set<T>().Where(x => x.ProjectId == projectId).OrderBy(x => x.Id).ToListAsync()) : Missing());
        api.MapGet(route + "/{id:guid}", async (Guid projectId, Guid id, DraupnirDb db) =>
            await db.Set<T>().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == id) is { } entity
                ? Results.Ok(entity) : Missing());
        api.MapPost(route, (Guid projectId, T input, DraupnirDb db) => SaveChild(projectId, null, input, resource, db));
        api.MapPut(route + "/{id:guid}", (Guid projectId, Guid id, T input, DraupnirDb db) => SaveChild(projectId, id, input, resource, db));
    }

    private static async Task<IResult> SaveChild<T>(Guid projectId, Guid? id, T input, string resource, DraupnirDb db)
        where T : class, IProjectChild
    {
        if (Validate(input) is { } invalid) return invalid;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.Projects.FindAsync(projectId) is not { } project) return Missing();
        var existing = id is null ? null : await db.Set<T>().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == id);
        if (id is not null && existing is null) return Missing();
        input.Id = id ?? Guid.NewGuid();
        input.ProjectId = projectId;
        if (input is Sprint sprint && (sprint.StartDate == default || sprint.EndDate < sprint.StartDate))
            return Invalid("endDate", "Provide a valid sprint start and end date.");
        if (input is Milestone milestone && milestone.DueDate == default)
            return Invalid("dueDate", "A due date is required.");
        if (input is WorkItem work)
        {
            if (!Regex.IsMatch(work.Code, "^" + project.Key + "-[1-9][0-9]*$"))
                return Invalid("code", $"Use {project.Key}-n.");
            if (work.StartDate > work.DueDate) return Invalid("dueDate", "Due date cannot precede start date.");
            if (!string.IsNullOrWhiteSpace(work.Severity) && !WorkItem.SeverityValues.Contains(work.Severity))
                return Invalid("severity", "Severity must be S1, S2, S3 or S4.");
            if (!await Matches<Requirement>(db, work.RequirementId, projectId))
                return Invalid("requirementId", "Requirement must belong to the project.");
            if (!await Matches<Sprint>(db, work.SprintId, projectId))
                return Invalid("sprintId", "Sprint must belong to the project.");
            if (!await Matches<Milestone>(db, work.MilestoneId, projectId))
                return Invalid("milestoneId", "Milestone must belong to the project.");
            if (await CheckStatus(db, work.Id, work.Status, work.BlockedReason) is { } issue) return issue;
            if (work.Status != WorkStatus.Blocked) work.BlockedReason = null;
        }
        if (existing is null) db.Add(input);
        else db.Entry(existing).CurrentValues.SetValues(input);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return id is null ? Results.Created($"/api/projects/{projectId}/{resource}/{input.Id}", input) : Results.Ok(input);
    }

    private static Task<bool> Matches<T>(DraupnirDb db, Guid? id, Guid projectId) where T : class, IProjectChild =>
        id is null ? Task.FromResult(true) : db.Set<T>().AnyAsync(x => x.Id == id && x.ProjectId == projectId);

    private static async Task<IResult> ListWorkItems(Guid projectId, WorkStatus? status, WorkType? type, Guid? sprintId,
        string? q, int? page, int? pageSize, DraupnirDb db)
    {
        var number = page ?? 1;
        var size = pageSize ?? 25;
        if (number < 1 || size is < 1 or > 100 || (long)(number - 1) * size > int.MaxValue)
            return Invalid("page", "Page must be positive and pageSize must be 1–100.");
        if ((status.HasValue && !Enum.IsDefined(status.Value)) || (type.HasValue && !Enum.IsDefined(type.Value)))
            return Invalid("filter", "Unknown work item status or type.");
        if (!await db.Projects.AnyAsync(x => x.Id == projectId)) return Missing();
        var query = db.WorkItems.Where(x => x.ProjectId == projectId);
        if (status is { } selectedStatus) query = query.Where(x => x.Status == selectedStatus);
        if (type is { } selectedType) query = query.Where(x => x.Type == selectedType);
        if (sprintId is { } selectedSprint) query = query.Where(x => x.SprintId == selectedSprint);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(x => x.Title.ToLower().Contains(term) || x.Code.ToLower().Contains(term)
                || x.Description.ToLower().Contains(term));
        }
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((number - 1) * size).Take(size).ToListAsync();
        return Results.Ok(new { items, total, page = number, pageSize = size });
    }

    private static async Task<IResult> AddDependency(Guid id, DependencyInput input, DraupnirDb db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.WorkItems.FindAsync(id) is not { } item) return Missing();
        if (input.DependsOnId is not { } target) return Invalid("dependsOnId", "A dependency is required.");
        if (await db.WorkItems.FindAsync(target) is not { } dependency || dependency.ProjectId != item.ProjectId)
            return Invalid("dependsOnId", "The dependency must belong to the same project.");
        var edges = await db.Dependencies.Where(edge =>
            db.WorkItems.Any(work => work.Id == edge.WorkItemId && work.ProjectId == item.ProjectId)).ToListAsync();
        if (Reaches(target, id, edges)) return Conflict("The dependency would create a cycle.");
        if (item.Status == WorkStatus.Done && dependency.Status != WorkStatus.Done)
            return Conflict("A completed item cannot depend on incomplete work.");
        if (edges.Any(edge => edge.WorkItemId == id && edge.DependsOnId == target))
            return Conflict("The dependency already exists.");
        var created = new WorkItemDependency { WorkItemId = id, DependsOnId = target };
        db.Dependencies.Add(created);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Created($"/api/work-items/{id}/dependencies/{target}", created);
    }

    public sealed record StatusChange(WorkStatus? Status, string? Reason);
    public sealed record DependencyInput(Guid? DependsOnId);
}
