using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Draupnir.Api;

public enum ProjectStatus { Active, Paused, Archived }
public enum Priority { P0, P1, P2, P3 }
public enum RequirementStatus { Draft, Approved, Implemented, Verified }
public enum RequirementSource { UserAuthorized, Repository, ClaudeDecision, External }
public enum WorkType { Task, Bug, Feature }
public enum WorkStatus { Backlog, Ready, InProgress, InReview, Blocked, Done }
public enum AssigneeKind { None, Human, Agent }
public enum SprintStatus { Planned, Active, Closed }
public enum RunSource { Supervisor, Gleipnir, Manual }
public enum EvidenceKind { Test, Build, Trace, Document, Log }
public enum EvidenceResult { Pass, Fail, NotTested, Blocked }
public enum Verdict { Pass, ChangesRequested }
public enum GateStatus { Pending, Approved, Rejected }
public enum UserRole { Admin, Member }

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    /// <summary>每次改密碼／角色／啟用狀態就換一個；舊 cookie 的 claim 不符就失效。</summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProjectMember
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public Guid? TargetId { get; set; }
    public string Detail { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [RegularExpression("^[A-Z]{2,10}$"), Required] public string Key { get; set; } = "";
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ProjectStatus Status { get; set; }
    public string? RepoPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IProjectChild
{
    Guid Id { get; set; }
    Guid ProjectId { get; set; }
}

public sealed class Requirement : IProjectChild
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    [Required, RegularExpression("^REQ-[1-9][0-9]*$")] public string Code { get; set; } = "";
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public Priority Priority { get; set; }
    public RequirementStatus Status { get; set; }
    public RequirementSource Source { get; set; }
}

public sealed class WorkItem : IProjectChild
{
    public static readonly string[] SeverityValues = ["S1", "S2", "S3", "S4"];
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    [Required, MaxLength(50)] public string Code { get; set; } = "";
    public WorkType Type { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public WorkStatus Status { get; set; }
    public Priority Priority { get; set; }
    public AssigneeKind AssigneeKind { get; set; }
    public string? Assignee { get; set; }
    public Guid? RequirementId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? MilestoneId { get; set; }
    [Range(0, (double)decimal.MaxValue)] public decimal? Estimate { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Severity { get; set; }
    public string? BlockedReason { get; set; }
}

public sealed class WorkItemDependency
{
    public Guid WorkItemId { get; set; }
    public Guid DependsOnId { get; set; }
}

public sealed class Sprint : IProjectChild
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Goal { get; set; } = "";
    public SprintStatus Status { get; set; }
}

public sealed class Milestone : IProjectChild
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    public DateOnly DueDate { get; set; }
    [Required] public string Status { get; set; } = "Planned";
}

public sealed class AgentRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string ExternalTaskId { get; set; } = "";
    public string Agent { get; set; } = "";
    public string Model { get; set; } = "";
    public string? SessionId { get; set; }
    public string Status { get; set; } = "";
    public int Attempt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public RunSource Source { get; set; }
}

public sealed class Evidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? AgentRunId { get; set; }
    public EvidenceKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string? Path { get; set; }
    public string? Url { get; set; }
    public EvidenceResult Result { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ImportKey { get; set; }
    public string? SpoolResultHash { get; set; }
    public string? Data { get; set; }
}

public sealed class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? WorkItemId { get; set; }
    public Guid? AgentRunId { get; set; }
    public string Reviewer { get; set; } = "";
    public Verdict Verdict { get; set; }
    public System.Text.Json.JsonElement Findings { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ImportKey { get; set; }
}

public sealed class HumanGate : IProjectChild
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string? ExternalRef { get; set; }
    public string Title { get; set; } = "";
    public GateStatus Status { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? Note { get; set; }
}

public sealed class DeliveryReport : IProjectChild
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string MarkdownBody { get; set; } = "";
    public string StylusSpecYaml { get; set; } = "";
    public string? DocxPath { get; set; }
}

public sealed class DraupnirDb(DbContextOptions<DraupnirDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemDependency> Dependencies => Set<WorkItemDependency>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<HumanGate> Gates => Set<HumanGate>();
    public DbSet<DeliveryReport> Reports => Set<DeliveryReport>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>().HasIndex(x => x.Email).IsUnique();
        model.Entity<ProjectMember>().HasKey(x => new { x.ProjectId, x.UserId });
        model.Entity<ProjectMember>().HasIndex(x => x.UserId);
        model.Entity<AuditLog>().HasIndex(x => x.CreatedAt);
        model.Entity<Project>().HasIndex(x => x.Key).IsUnique();
        model.Entity<Requirement>().HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();
        model.Entity<WorkItem>().HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();
        model.Entity<Sprint>().HasIndex(x => x.ProjectId).IsUnique().HasFilter("\"Status\" = 1");
        model.Entity<AgentRun>().HasIndex(x => new { x.Source, x.ExternalTaskId, x.Attempt }).IsUnique();
        model.Entity<Evidence>().HasIndex(x => new { x.AgentRunId, x.ImportKey }).IsUnique();
        model.Entity<Evidence>().HasIndex(x => new { x.ProjectId, x.SpoolResultHash }).IsUnique()
            .HasFilter("\"SpoolResultHash\" IS NOT NULL");
        model.Entity<Evidence>().Property(x => x.Data).HasColumnType("jsonb");
        model.Entity<Review>().HasIndex(x => new { x.AgentRunId, x.ImportKey }).IsUnique();
        model.Entity<Review>().Property(x => x.Findings).HasColumnType("jsonb");
        model.Entity<HumanGate>().HasIndex(x => new { x.ProjectId, x.ExternalRef }).IsUnique();
        model.Entity<HumanGate>().Property(x => x.Status).IsConcurrencyToken();
        model.Entity<WorkItemDependency>().HasKey(x => new { x.WorkItemId, x.DependsOnId });

        foreach (var type in new[] { typeof(Requirement), typeof(WorkItem), typeof(Sprint), typeof(Milestone),
                     typeof(HumanGate), typeof(DeliveryReport), typeof(AgentRun) })
            model.Entity(type).HasOne(typeof(Project)).WithMany().HasForeignKey("ProjectId").OnDelete(DeleteBehavior.Restrict);

        model.Entity<WorkItem>().HasOne<Requirement>().WithMany().HasForeignKey(x => x.RequirementId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WorkItem>().HasOne<Sprint>().WithMany().HasForeignKey(x => x.SprintId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WorkItem>().HasOne<Milestone>().WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WorkItemDependency>().HasOne<WorkItem>().WithMany().HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<WorkItemDependency>().HasOne<WorkItem>().WithMany().HasForeignKey(x => x.DependsOnId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Evidence>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ProjectMember>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ProjectMember>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AuditLog>().HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        foreach (var type in new[] { typeof(AgentRun), typeof(Evidence), typeof(Review), typeof(HumanGate) })
            model.Entity(type).HasOne(typeof(WorkItem)).WithMany().HasForeignKey("WorkItemId").OnDelete(DeleteBehavior.Restrict);
        model.Entity<Evidence>().HasOne<AgentRun>().WithMany().HasForeignKey(x => x.AgentRunId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Review>().HasOne<AgentRun>().WithMany().HasForeignKey(x => x.AgentRunId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<HumanGate>().HasOne<User>().WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
