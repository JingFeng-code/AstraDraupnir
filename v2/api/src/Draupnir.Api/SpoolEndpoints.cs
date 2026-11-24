using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Draupnir.Api.ApiRules;

namespace Draupnir.Api;

/// <summary>Read-only Spool result import. The payload is stored as Trace evidence.</summary>
public static class SpoolEndpoints
{
    public static void MapSpoolEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/projects/{projectId:guid}/spool-results", Import);
        api.MapGet("/projects/{projectId:guid}/spool-results", async (Guid projectId, DraupnirDb db) =>
            await db.Projects.AnyAsync(p => p.Id == projectId)
                ? Results.Ok(await db.Evidence.Where(e => e.ProjectId == projectId && e.SpoolResultHash != null)
                    .OrderByDescending(e => e.RecordedAt).ToListAsync())
                : Missing());
    }

    private static async Task<IResult> Import(Guid projectId, HttpRequest request, DraupnirDb db)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Missing();
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        if (body.Length == 0) return Invalid("body", "A spool-result/v1 JSON object is required.");
        if (body.Length > 1_000_000) return Invalid("body", "The payload is too large (max 1 MB).");

        JsonDocument document;
        try { document = JsonDocument.Parse(body); }
        catch (JsonException error) { return Invalid("body", $"Invalid JSON: {error.Message}"); }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryString(root, "schema", out var schema) || schema != "spool-result/v1")
                return Invalid("schema", "Expected schema spool-result/v1.");
            if (!TryString(root, "command", out var command) || string.IsNullOrWhiteSpace(command))
                return Invalid("command", "A non-empty command is required.");
            if (!TryString(root, "status", out var status) || status is not ("pass" or "fail"))
                return Invalid("status", "Status must be pass or fail.");
            if (!root.TryGetProperty("exitCode", out var exitCode) || exitCode.ValueKind != JsonValueKind.Number)
                return Invalid("exitCode", "exitCode must be a number.");
            if (!root.TryGetProperty("findings", out var findings) || findings.ValueKind != JsonValueKind.Array)
                return Invalid("findings", "findings must be an array.");
            if (!root.TryGetProperty("counts", out var counts) || counts.ValueKind != JsonValueKind.Object)
                return Invalid("counts", "counts must be an object.");
            if (root.TryGetProperty("affectedTraces", out var affected) && affected.ValueKind != JsonValueKind.Array)
                return Invalid("affectedTraces", "affectedTraces must be an array when present.");

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
            var existing = await db.Evidence.FirstOrDefaultAsync(e => e.ProjectId == projectId && e.SpoolResultHash == hash);
            if (existing is not null)
                return Results.Ok(new { imported = false, evidenceId = existing.Id, hash });

            var evidence = new Evidence
            {
                ProjectId = projectId,
                Kind = EvidenceKind.Trace,
                Title = command,
                Path = "spool-result/v1",
                Result = status == "pass" ? EvidenceResult.Pass : EvidenceResult.Fail,
                SpoolResultHash = hash,
                Data = body,
                RecordedAt = DateTimeOffset.UtcNow
            };
            db.Evidence.Add(evidence);
            await db.SaveChangesAsync();
            return Results.Created($"/api/evidence/{evidence.Id}", new { imported = true, evidenceId = evidence.Id, hash });
        }
    }

    private static bool TryString(JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString() ?? "";
        return true;
    }
}
