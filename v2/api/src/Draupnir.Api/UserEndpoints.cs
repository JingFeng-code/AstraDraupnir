using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Draupnir.Api.ApiRules;
using System.Data;

namespace Draupnir.Api;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/users", async (ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } actor) return Access.Unauthorized();
            if (!Access.IsAdmin(actor)) return Access.Forbidden();
            return Results.Ok(await db.Users.OrderBy(u => u.Email).Select(u => new
            {
                u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.CreatedAt
            }).ToListAsync());
        });

        api.MapPost("/users", async (UserInput input, ClaimsPrincipal principal, DraupnirDb db, IPasswordHasher<User> hasher) =>
        {
            if (await Access.Current(db, principal) is not { } actor) return Access.Unauthorized();
            if (!Access.IsAdmin(actor)) return Access.Forbidden();
            var email = input.Email?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(input.DisplayName))
                return Invalid("email", "Email and display name are required.");
            if (string.IsNullOrWhiteSpace(input.Password) || input.Password.Length < 8)
                return Invalid("password", "Password must be at least 8 characters.");
            if (await db.Users.AnyAsync(u => u.Email == email)) return Conflict("Email is already registered.");
            var role = input.Role ?? UserRole.Member;
            if (!Enum.IsDefined(role)) return Invalid("role", "Role must be Admin or Member.");
            var user = new User { Email = email, DisplayName = input.DisplayName.Trim(), Role = role, IsActive = true };
            user.PasswordHash = hasher.HashPassword(user, input.Password);
            db.Users.Add(user);
            await Access.Audit(db, actor.Id, "user.create", "user", user.Id, $"email={email}; role={role}");
            await db.SaveChangesAsync();
            return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAt });
        });

        api.MapPatch("/users/{id:guid}", async (Guid id, UserPatch input, ClaimsPrincipal principal, DraupnirDb db, IPasswordHasher<User> hasher) =>
        {
            if (await Access.Current(db, principal) is not { } actor) return Access.Unauthorized();
            if (!Access.IsAdmin(actor)) return Access.Forbidden();
            // 最後一位有效 Admin 的 invariant 必須在交易內檢查：兩個同時送出的停用／降階請求
            // 若各自讀到「還有 2 位」，就會同時通過。SERIALIZABLE 讓其中一個在提交時失敗（409）。
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.Users.FindAsync(id) is not { } user) return Missing();
            if (input.DisplayName is null && input.Role is null && input.IsActive is null && input.Password is null)
                return Invalid("body", "Provide at least one field to change.");
            var nextRole = input.Role ?? user.Role;
            var nextActive = input.IsActive ?? user.IsActive;
            if (!Enum.IsDefined(nextRole)) return Invalid("role", "Role must be Admin or Member.");
            if (user.Role == UserRole.Admin && (!nextActive || nextRole != UserRole.Admin)
                && await db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive) <= 1)
                return Conflict("At least one active administrator is required.");
            if (input.DisplayName is not null)
            {
                if (string.IsNullOrWhiteSpace(input.DisplayName)) return Invalid("displayName", "Display name is required.");
                user.DisplayName = input.DisplayName.Trim();
            }
            user.Role = nextRole;
            user.IsActive = nextActive;
            if (input.Password is not null)
            {
                if (input.Password.Length < 8) return Invalid("password", "Password must be at least 8 characters.");
                user.PasswordHash = hasher.HashPassword(user, input.Password);
            }
            await Access.Audit(db, actor.Id, "user.update", "user", user.Id,
                $"role={user.Role}; active={user.IsActive}; passwordReset={input.Password is not null}");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAt });
        });

        api.MapGet("/audit", async (ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } actor) return Access.Unauthorized();
            if (!Access.IsAdmin(actor)) return Access.Forbidden();
            return Results.Ok(await db.AuditLogs.OrderByDescending(a => a.CreatedAt).Take(200).ToListAsync());
        });

        api.MapGet("/projects/{projectId:guid}/members", async (Guid projectId, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } actor) return Access.Unauthorized();
            if (!Access.IsAdmin(actor)) return Access.Forbidden();
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Missing();
            return Results.Ok(await db.ProjectMembers.Where(m => m.ProjectId == projectId)
                .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { u.Id, u.Email, u.DisplayName, u.Role, u.IsActive })
                .OrderBy(u => u.Email).ToListAsync());
        });

        api.MapPost("/projects/{projectId:guid}/members", async (Guid projectId, MemberInput input, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } actor) return Access.Unauthorized();
            if (!Access.IsAdmin(actor)) return Access.Forbidden();
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Missing();
            if (await db.Users.FindAsync(input.UserId) is not { IsActive: true }) return Invalid("userId", "An active user is required.");
            if (!await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == input.UserId))
            {
                db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = input.UserId });
                await Access.Audit(db, actor.Id, "membership.add", "project", projectId, $"userId={input.UserId}");
                await db.SaveChangesAsync();
            }
            return Results.Ok(new { projectId, userId = input.UserId });
        });

        api.MapDelete("/projects/{projectId:guid}/members/{userId:guid}", async (Guid projectId, Guid userId, ClaimsPrincipal principal, DraupnirDb db) =>
        {
            if (await Access.Current(db, principal) is not { } actor) return Access.Unauthorized();
            if (!Access.IsAdmin(actor)) return Access.Forbidden();
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Missing();
            if (await db.ProjectMembers.FindAsync(projectId, userId) is not { } member) return Missing();
            db.ProjectMembers.Remove(member);
            await Access.Audit(db, actor.Id, "membership.remove", "project", projectId, $"userId={userId}");
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    public sealed record UserInput(string? Email, string? DisplayName, string? Password, UserRole? Role);
    public sealed record UserPatch(string? DisplayName, UserRole? Role, bool? IsActive, string? Password);
    public sealed record MemberInput(Guid UserId);
}
