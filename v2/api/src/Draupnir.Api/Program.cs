using System.Security.Claims;
using System.Text.Json.Serialization;
using Draupnir.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<DraupnirDb>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Draupnir")
    ?? "Host=127.0.0.1;Port=55432;Database=draupnir;Username=postgres"));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "draupnir.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    // 本機開發走 HTTP；正式部署請用 TLS 並把 SecurePolicy 改成 Always（見 v2/README 的安全政策）。
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (BadHttpRequestException error) when (!context.Response.HasStarted && error.StatusCode == 400)
    {
        var json = error.InnerException as System.Text.Json.JsonException;
        var field = json?.Path?.TrimStart('$', '.');
        await ApiRules.Invalid(string.IsNullOrWhiteSpace(field) ? "request" : field, "Invalid or missing request value.")
            .ExecuteAsync(context);
    }
    catch (Exception error) when (!context.Response.HasStarted && ApiRules.IsDatabaseConflict(error))
    {
        await Results.Problem("The change conflicts with an existing record or concurrent operation.", statusCode: 409)
            .ExecuteAsync(context);
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
// Anonymous, non-secret liveness/disposability marker for the E2E write guard.
app.MapGet("/api/health", async (DraupnirDb db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    var database = db.Database.GetDbConnection().Database ?? "";
    var normalized = database.ToLowerInvariant();
    var preview = normalized is "draupnir" or "draupnir_v21" or "draupnir_design";
    var disposable = !preview && (normalized.StartsWith("draupnir_test_") ||
        normalized.EndsWith("_e2e") || normalized.EndsWith("_test") ||
        normalized.EndsWith("_fix2") || normalized.EndsWith("_accept"));
    return Results.Ok(new { status = canConnect ? "ok" : "unavailable", database, disposable });
});
var api = app.MapGroup("/api").RequireAuthorization();

api.MapPost("/auth/login", async (Login input, DraupnirDb db, IPasswordHasher<User> hasher, HttpContext context) =>
{
    var email = input.Email?.Trim().ToLowerInvariant();
    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(input.Password))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Email and password are required."] });
    // 失敗節流（以 audit 記錄為準，不影響成功登入）：同一 email 10 分鐘內失敗 5 次就 429。
    var windowStart = DateTimeOffset.UtcNow.AddMinutes(-10);
    var recentFailures = await db.AuditLogs.CountAsync(row =>
        row.Action == "auth.login.failed" && row.Detail == $"email={email}" && row.CreatedAt >= windowStart);
    if (recentFailures >= 5)
        return Results.Problem("Too many failed sign-in attempts. Try again later.", statusCode: 429);
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
    if (user is null)
    {
        db.AuditLogs.Add(new AuditLog { ActorUserId = null, Action = "auth.login.failed", TargetType = "user", TargetId = null, Detail = $"email={email}" });
        await db.SaveChangesAsync();
        return Results.Problem("Invalid credentials.", statusCode: 401);
    }
    if (!user.IsActive || hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
    {
        db.AuditLogs.Add(new AuditLog { ActorUserId = user.Id, Action = "auth.login.failed", TargetType = "user", TargetId = user.Id, Detail = $"email={email}" });
        await db.SaveChangesAsync();
        return Results.Problem("Invalid credentials.", statusCode: 401);
    }
    await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.DisplayName),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim("draupnir:stamp", user.SecurityStamp.ToString())
    ], CookieAuthenticationDefaults.AuthenticationScheme)));
    db.AuditLogs.Add(new AuditLog { ActorUserId = user.Id, Action = "auth.login", TargetType = "user", TargetId = user.Id, Detail = "local password login" });
    await db.SaveChangesAsync();
    return Results.Ok(new { user.Id, user.Email, user.DisplayName, user.Role, user.IsActive });
}).AllowAnonymous();
api.MapPost("/auth/logout", async (HttpContext context, ClaimsPrincipal principal, DraupnirDb db) =>
{
    if (Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        && await db.Users.FindAsync(userId) is { } user)
    {
        db.AuditLogs.Add(new AuditLog { ActorUserId = user.Id, Action = "auth.logout", TargetType = "user", TargetId = user.Id, Detail = $"email={user.Email}" });
        await db.SaveChangesAsync();
    }
    await context.SignOutAsync();
    return Results.NoContent();
});
api.MapGet("/auth/me", async (ClaimsPrincipal principal, DraupnirDb db) =>
{
    // 與其他端點用同一套檢查：未啟用或 security stamp 已被輪替（改密碼／角色）都視為未登入。
    if (await Access.Current(db, principal) is not { } user) return Results.Problem(statusCode: 401);
    return Results.Ok(new { user.Id, user.Email, user.DisplayName, user.Role, user.IsActive });
});

api.MapProjectEndpoints();
api.MapDeliveryEndpoints();
api.MapSpoolEndpoints();
api.MapUserEndpoints();

if (args.Contains("--seed-demo") || !EF.IsDesignTime)
{
    await using var scope = app.Services.CreateAsyncScope();
    await Seeds.Initialize(scope.ServiceProvider, app.Configuration, app.Logger, args.Contains("--seed-demo"));
}
if (args.Contains("--seed-demo")) return;
app.Run();

public sealed record Login(string? Email, string? Password);
public partial class Program { }
