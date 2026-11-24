using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Draupnir.Api;

public static class Seeds
{
    public static async Task Initialize(IServiceProvider services, IConfiguration config, ILogger logger, bool demo)
    {
        var email = config["DRAUPNIR_ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
        var password = config["DRAUPNIR_ADMIN_PASSWORD"];
        var db = services.GetRequiredService<DraupnirDb>();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            logger.LogWarning("Draupnir administrator was not seeded because one or both administrator settings are absent.");
        else
        {
            await using var enrollment = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existing = await db.Users.OrderBy(user => user.CreatedAt).FirstOrDefaultAsync();
            if (existing is null)
            {
                var user = new User { Email = email, DisplayName = "Administrator" };
                user.PasswordHash = services.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, password);
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
            else if (existing.Email != email)
                logger.LogWarning("Draupnir already has an administrator; the configured address was not enrolled.");
            await enrollment.CommitAsync();
        }
        if (!demo || await db.Projects.AnyAsync(project => project.Key == "DEMO")) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var project = new Project { Key = "DEMO", Name = "Synthetic delivery project", Description = "Local demonstration only." };
        var requirement = new Requirement { Id = Guid.NewGuid(), ProjectId = project.Id, Code = "REQ-1",
            Title = "Demonstrate the delivery workflow", Source = RequirementSource.UserAuthorized };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sprint = new Sprint { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Demo sprint",
            StartDate = today, EndDate = today.AddDays(7), Status = SprintStatus.Active };
        var item = new WorkItem { Id = Guid.NewGuid(), ProjectId = project.Id, Code = "DEMO-1", Title = "Build a synthetic example",
            RequirementId = requirement.Id, SprintId = sprint.Id, Status = WorkStatus.Ready };
        db.AddRange(project, requirement, sprint, item, new HumanGate { Id = Guid.NewGuid(), ProjectId = project.Id,
            WorkItemId = item.Id, Title = "Review the demonstration" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
