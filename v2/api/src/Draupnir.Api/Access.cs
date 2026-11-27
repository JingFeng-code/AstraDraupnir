using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Draupnir.Api;

/// <summary>Small policy helper shared by the endpoint groups.</summary>
public static class Access
{
    public static async Task<User?> Current(DraupnirDb db, ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        var user = await db.Users.FindAsync(id);
        if (user is not { IsActive: true }) return null;
        // 舊 cookie 沒有 stamp，或 stamp 已被輪替（改密碼／角色／啟用狀態）就視為未登入。
        var stamp = principal.FindFirstValue("draupnir:stamp");
        return stamp is not null && Guid.TryParse(stamp, out var value) && value == user.SecurityStamp ? user : null;
    }

    public static bool IsAdmin(User user) => user.Role == UserRole.Admin;

    public static async Task<bool> CanProject(DraupnirDb db, User user, Guid projectId) =>
        IsAdmin(user) || await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == user.Id);

    public static async Task<List<Guid>> VisibleProjectIds(DraupnirDb db, User user) =>
        IsAdmin(user)
            ? await db.Projects.Select(p => p.Id).ToListAsync()
            : await db.ProjectMembers.Where(m => m.UserId == user.Id).Select(m => m.ProjectId).ToListAsync();

    public static IResult Unauthorized() => Results.Problem("Sign in required.", statusCode: 401);
    public static IResult Forbidden() => Results.Problem("You do not have access to this resource.", statusCode: 403);

    public static async Task Audit(DraupnirDb db, Guid actorId, string action, string targetType, Guid? targetId, string detail = "")
    {
        db.AuditLogs.Add(new AuditLog { ActorUserId = actorId, Action = action, TargetType = targetType, TargetId = targetId, Detail = detail });
        await Task.CompletedTask;
    }
}
