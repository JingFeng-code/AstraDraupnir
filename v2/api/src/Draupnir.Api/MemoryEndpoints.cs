using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Draupnir.Api.ApiRules;

namespace Draupnir.Api;

public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this RouteGroupBuilder api) =>
        api.MapGet("/projects/{projectId:guid}/memory", Search);

    private static async Task<IResult> Search(Guid projectId, string? q, ClaimsPrincipal principal,
        DraupnirDb db, IConfiguration config, CancellationToken cancellation)
    {
        if (await Access.Current(db, principal) is not { } user) return Access.Unauthorized();
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, cancellation)) return Missing();
        if (!await Access.CanProject(db, user, projectId)) return Access.Forbidden();
        if (string.IsNullOrWhiteSpace(q) || q.Length > 200) return Invalid("q", "Use 1 to 200 characters.");

        var root = config["EngineeringMemory:Root"];
        var script = config["EngineeringMemory:Script"];
        var python = config["EngineeringMemory:Python"];
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(script) || string.IsNullOrWhiteSpace(python))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var scope = projectId.ToString("D");
        var file = Path.Combine(root, scope + ".sqlite");
        if (!File.Exists(file)) return Results.Ok(new { status = "no_results", results = Array.Empty<object>() });

        var start = new ProcessStartInfo(python)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "-X", "utf8", script, "--db", file, "--project", scope, "search", "--", q })
            start.ArgumentList.Add(argument);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start memory CLI.");
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                await errors;
                var json = await output;
                if (process.ExitCode != 0) return Results.StatusCode(StatusCodes.Status502BadGateway);
                using var document = JsonDocument.Parse(json);
                return Results.Ok(document.RootElement.Clone());
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or JsonException)
        {
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
