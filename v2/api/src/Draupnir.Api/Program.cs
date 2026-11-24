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
var api = app.MapGroup("/api").RequireAuthorization();

api.MapPost("/auth/login", async (Login input, DraupnirDb db, IPasswordHasher<User> hasher, HttpContext context) =>
{
    var email = input.Email?.Trim().ToLowerInvariant();
    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(input.Password))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Email and password are required."] });
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
    if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        return Results.Problem("Invalid credentials.", statusCode: 401);
    await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.DisplayName)
    ], CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Ok(new { user.Id, user.Email, user.DisplayName });
}).AllowAnonymous();
api.MapPost("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return Results.NoContent();
});
api.MapGet("/auth/me", async (ClaimsPrincipal principal, DraupnirDb db) =>
{
    var user = await db.Users.FindAsync(Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!));
    return user is null ? Results.Problem(statusCode: 401) : Results.Ok(new { user.Id, user.Email, user.DisplayName });
});

api.MapProjectEndpoints();
api.MapDeliveryEndpoints();

if (args.Contains("--seed-demo") || !EF.IsDesignTime)
{
    await using var scope = app.Services.CreateAsyncScope();
    await Seeds.Initialize(scope.ServiceProvider, app.Configuration, app.Logger, args.Contains("--seed-demo"));
}
if (args.Contains("--seed-demo")) return;
app.Run();

public sealed record Login(string? Email, string? Password);
public partial class Program { }
