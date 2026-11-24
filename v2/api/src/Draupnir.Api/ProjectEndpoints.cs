using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using static Draupnir.Api.ApiRules;

namespace Draupnir.Api;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/projects", async (ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            var query = db.Projects.AsQueryable();
            if (!Access.IsAdmin(user))
                query = query.Where(p => db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == user.Id));
            return Results.Ok(await query.OrderBy(x => x.Key).ToListAsync());
        });

        api.MapGet("/projects/{id:guid}", async (Guid id, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (await db.Projects.FindAsync(id) is not { } project) return Missing();
            if (!await Access.CanProject(db, user, id)) return Access.Forbidden();
            return Results.Ok(project);
        });

        api.MapPost("/projects", async (Project input, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (!Access.IsAdmin(user)) return Access.Forbidden();
            if (Validate(input) is { } invalid) return invalid;
            input.Id = Guid.NewGuid();
            input.CreatedAt = input.UpdatedAt = DateTimeOffset.UtcNow;
            db.Projects.Add(input);
            await Access.Audit(db, user.Id, "project.create", "project", input.Id, $"key={input.Key}");
            await db.SaveChangesAsync();
            return Results.Created($"/api/projects/{input.Id}", input);
        });

        api.MapPut("/projects/{id:guid}", async (Guid id, ProjectUpdateInput input, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.Projects.FindAsync(id) is not { } project) return Missing();
            if (!await Access.CanProject(db, user, id)) return Access.Forbidden();
            if (Validate(input) is { } invalid) return invalid;
            // 狀態（封存／解除封存）只有 Admin 能改；一般成員只能改名稱／描述／repo 路徑。
            var requested = input.Status;
            var statusChanged = requested is { } value && value != project.Status;
            if (statusChanged && !Access.IsAdmin(user)) return Access.Forbidden();
            if (project.Key != input.Key && await db.WorkItems.AnyAsync(x => x.ProjectId == id))
                return Conflict("The project key cannot change after work item codes exist.");
            var previousStatus = project.Status;
            project.Key = input.Key;
            project.Name = input.Name;
            project.Description = input.Description ?? "";
            project.RepoPath = input.RepoPath;
            if (statusChanged) project.Status = requested!.Value;
            project.UpdatedAt = DateTimeOffset.UtcNow;
            await Access.Audit(db, user.Id, statusChanged ? "project.status" : "project.update", "project", project.Id,
                statusChanged
                    ? $"key={project.Key};status={previousStatus}->{project.Status}"
                    : $"key={project.Key}");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(project);
        });

        api.MapDelete("/projects/{id:guid}", async (Guid id, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (!Access.IsAdmin(user)) return Access.Forbidden();
            if (await db.Projects.FindAsync(id) is not { } project) return Missing();
            if (project.Status != ProjectStatus.Archived)
            {
                project.Status = ProjectStatus.Archived;
                project.UpdatedAt = DateTimeOffset.UtcNow;
                await Access.Audit(db, user.Id, "project.archive", "project", project.Id, $"key={project.Key}");
                await db.SaveChangesAsync();
            }
            return Results.NoContent();
        });

        MapChildren<Requirement>(api, "requirements");
        MapChildren<WorkItem>(api, "work-items", list: false);
        MapChildren<Sprint>(api, "sprints");
        MapChildren<Milestone>(api, "milestones");
        api.MapGet("/projects/{projectId:guid}/work-items", ListWorkItems);
        api.MapGet("/work-items/{id:guid}/dependencies", async (Guid id, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (await db.WorkItems.FindAsync(id) is not { } item) return Missing();
            if (!await Access.CanProject(db, user, item.ProjectId)) return Access.Forbidden();
            return Results.Ok(await db.Dependencies.Where(x => x.WorkItemId == id).ToListAsync());
        });
        api.MapPost("/work-items/{id:guid}/dependencies", AddDependency);
        api.MapDelete("/work-items/{id:guid}/dependencies/{depId:guid}", async (Guid id, Guid depId, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (await db.WorkItems.FindAsync(id) is not { } item) return Missing();
            if (!await Access.CanProject(db, user, item.ProjectId)) return Access.Forbidden();
            if (await db.Dependencies.FindAsync(id, depId) is not { } dependency) return Missing();
            db.Dependencies.Remove(dependency);
            await Access.Audit(db, user.Id, "dependency.remove", "work_item", item.Id, $"dependsOn={depId}");
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        api.MapPatch("/work-items/{id:guid}/status", async (Guid id, StatusChange input, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (input.Status is not { } status) return Invalid("status", "A status is required.");
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.WorkItems.FindAsync(id) is not { } item) return Missing();
            if (!await Access.CanProject(db, user, item.ProjectId)) return Access.Forbidden();
            if (await CheckStatus(db, id, status, input.Reason) is { } issue) return issue;
            item.Status = status;
            item.BlockedReason = status == WorkStatus.Blocked ? input.Reason : null;
            await Access.Audit(db, user.Id, "work_item.status", "work_item", item.Id, $"status={status}");
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
            api.MapGet(route, async (Guid projectId, ClaimsPrincipal principal, DraupnirDb db) =>
            {
                if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
                if (!await db.Projects.AnyAsync(x => x.Id == projectId)) return Missing();
                if (!await Access.CanProject(db, user, projectId)) return Access.Forbidden();
                return Results.Ok(await db.Set<T>().Where(x => x.ProjectId == projectId).OrderBy(x => x.Id).ToListAsync());
            });
        api.MapGet(route + "/{id:guid}", async (Guid projectId, Guid id, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
            if (!await db.Projects.AnyAsync(x => x.Id == projectId)) return Missing();
            if (!await Access.CanProject(db, user, projectId)) return Access.Forbidden();
            return await db.Set<T>().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == id) is { } entity
                ? Results.Ok(entity) : Missing();
        });
        api.MapPost(route, (Guid projectId, T input, ClaimsPrincipal principal, DraupnirDb db) =>
            SaveChild(projectId, null, input, resource, principal, db));
        api.MapPut(route + "/{id:guid}", (Guid projectId, Guid id, T input, ClaimsPrincipal principal, DraupnirDb db) =>
            SaveChild(projectId, id, input, resource, principal, db));
    }

    private static async Task<IResult> SaveChild<T>(Guid projectId, Guid? id, T input, string resource,
        ClaimsPrincipal principal, DraupnirDb db) where T : class, IProjectChild
    {
        if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
        if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Missing();
        if (!await Access.CanProject(db, user, projectId)) return Access.Forbidden();
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
        var action = resource.TrimEnd('s').Replace("-", "_") + (id is null ? ".create" : ".update");
        var code = input switch { Requirement r => r.Code, WorkItem w => w.Code, Sprint s => s.Name, Milestone m => m.Name, _ => input.Id.ToString() };
        await Access.Audit(db, user.Id, action, resource, input.Id, $"code={code}");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return id is null ? Results.Created($"/api/projects/{projectId}/{resource}/{input.Id}", input) : Results.Ok(input);
    }

    private static Task<bool> Matches<T>(DraupnirDb db, Guid? id, Guid projectId) where T : class, IProjectChild =>
        id is null ? Task.FromResult(true) : db.Set<T>().AnyAsync(x => x.Id == id && x.ProjectId == projectId);

    private static async Task<IResult> ListWorkItems(Guid projectId, WorkStatus? status, WorkType? type, Guid? sprintId,
        string? q, int? page, int? pageSize, ClaimsPrincipal principal, DraupnirDb db)
    {
        if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
        if (!await db.Projects.AnyAsync(x => x.Id == projectId)) return Missing();
        if (!await Access.CanProject(db, user, projectId)) return Access.Forbidden();
        var number = page ?? 1;
        var size = pageSize ?? 25;
        if (number < 1 || size is < 1 or > 100 || (long)(number - 1) * size > int.MaxValue)
            return Invalid("page", "Page must be positive and pageSize must be 1–100.");
        if ((status.HasValue && !Enum.IsDefined(status.Value)) || (type.HasValue && !Enum.IsDefined(type.Value)))
            return Invalid("filter", "Unknown work item status or type.");
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

    private static async Task<IResult> AddDependency(Guid id, DependencyInput input, ClaimsPrincipal principal, DraupnirDb db)
    {
        if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.WorkItems.FindAsync(id) is not { } item) return Missing();
        if (!await Access.CanProject(db, user, item.ProjectId)) return Access.Forbidden();
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
        await Access.Audit(db, user.Id, "dependency.add", "work_item", id, $"dependsOn={target}");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Created($"/api/work-items/{id}/dependencies/{target}", created);
    }

    public sealed record StatusChange(WorkStatus? Status, string? Reason);

    /// <summary>PUT /projects/{id} 的專用輸入：Id／CreatedAt 不可由用戶端指定，狀態變更另外檢查 Admin。</summary>
    public sealed record ProjectUpdateInput(
        [property: System.ComponentModel.DataAnnotations.Required]
        [property: System.ComponentModel.DataAnnotations.RegularExpression("^[A-Z]{2,10}$")] string Key,
        [property: System.ComponentModel.DataAnnotations.Required]
        [property: System.ComponentModel.DataAnnotations.MaxLength(200)] string Name,
        string? Description,
        string? RepoPath,
        ProjectStatus? Status);
    public sealed record DependencyInput(Guid? DependsOnId);
}
