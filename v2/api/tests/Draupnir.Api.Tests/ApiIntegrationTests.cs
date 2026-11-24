using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Draupnir.Api.Tests;

public sealed class ApiIntegrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task RequiresCookieAndSupportsLoginMeLogout()
    {
        using var client = NewClient();
        var denied = await client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal("application/problem+json", denied.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@example.test", password = "wrong" })).StatusCode);
        var login = await Login(client);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("admin@example.test", (await Json(await client.GetAsync("/api/auth/me"))).GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var openApi = await Json(await client.GetAsync("/openapi/v1.json"));
        Assert.True(openApi.GetProperty("paths").TryGetProperty("/api/import/supervisor", out _));
    }

    [Fact]
    public async Task InvalidJsonFieldsReturnValidationProblem()
    {
        using var client = await AuthorizedClient();
        var response = await client.PostAsJsonAsync("/api/projects", new { key = "FAULT", name = "Invalid", status = "Unknown" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response, requireSuccess: false)).GetProperty("errors").TryGetProperty("status", out _));
    }

    [Fact]
    public async Task ProjectsCreateReadUpdateAndArchive()
    {
        using var client = await AuthorizedClient();
        var id = await NewProject(client, "CRUD");
        var route = $"/api/projects/{id}";
        var project = await Json(await client.GetAsync(route));
        Assert.Equal("CRUD", project.GetProperty("key").GetString());
        var updated = await Json(await client.PutAsJsonAsync(route, new { key = "CRUD", name = "Updated", status = "Paused" }));
        Assert.Equal("Updated", updated.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(route)).StatusCode);
        Assert.Equal("Archived", (await Json(await client.GetAsync(route))).GetProperty("status").GetString());
        Assert.Contains((await Json(await client.GetAsync("/api/projects"))).EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/projects", new { key = "CRUD", name = "Duplicate" })).StatusCode);
    }

    [Fact]
    public async Task RequirementCodesAndWorkItemCodesAreUniqueWithinProject()
    {
        using var client = await AuthorizedClient();
        var id = await NewProject(client, "CODES");
        var route = $"/api/projects/{id}/requirements";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(route,
            new { code = "REQ-1", title = "One", priority = "P2", source = "Repository" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { code = "REQ-1", title = "Duplicate" })).StatusCode);
        var bad = await client.PostAsJsonAsync(route, new { code = "WRONG", title = "Bad code" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.True((await Json(bad, requireSuccess: false)).GetProperty("errors").TryGetProperty("code", out _));
        await NewWork(client, id, "CODES-1", "Original");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/projects/{id}/work-items",
            new { code = "CODES-1", title = "Duplicate" })).StatusCode);
        var other = await NewProject(client, "OTHER");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/projects/{other}/requirements",
            new { code = "REQ-1", title = "Independent" })).StatusCode);
    }

    [Fact]
    public async Task DependenciesRejectCyclesAndEnforceDoneAndBlockedRules()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "GRAPH");
        var a = await NewWork(client, project, "GRAPH-1", "Parent");
        var b = await NewWork(client, project, "GRAPH-2", "Dependency");
        var c = await NewWork(client, project, "GRAPH-3", "Transitive");
        Assert.Equal(HttpStatusCode.Created, (await AddDependency(client, a, b)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AddDependency(client, b, c)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AddDependency(client, c, a)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AddDependency(client, a, a)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PatchStatus(client, a, "Done")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PatchStatus(client, c, "Blocked")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatus(client, c, "Blocked", "Waiting for input")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatus(client, c, "Done")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatus(client, b, "Done")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatus(client, a, "Done")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PatchStatus(client, b, "Ready")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/work-items/{a}/dependencies/{b}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatus(client, b, "Ready")).StatusCode);
    }

    [Fact]
    public async Task SingleActiveSprintAndMilestoneAssociationsAreEnforced()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "SPRINT");
        var route = $"/api/projects/{project}/sprints";
        var sprint = await Json(await client.PostAsJsonAsync(route, SprintInput("First")));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, SprintInput("Second"))).StatusCode);
        var other = await NewProject(client, "NEIGHBOR");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/projects/{other}/work-items",
            new { code = "NEIGHBOR-1", title = "Cross-project", sprintId = sprint.GetProperty("id").GetGuid() })).StatusCode);
        var milestones = $"/api/projects/{project}/milestones";
        var created = await Json(await client.PostAsJsonAsync(milestones, new { name = "Release", dueDate = "2026-11-30" }));
        Assert.Equal("Planned", created.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{milestones}/{created.GetProperty("id")}",
            new { name = "Release updated", status = "Active", dueDate = "2026-12-01" })).StatusCode);
    }

    [Fact]
    public async Task GateDecisionRequiresHumanCookieAndCannotBeReplaced()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "GATE");
        var gate = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
            new { title = "Confirm delivery", status = "Approved" }));
        Assert.Equal("Pending", gate.GetProperty("status").GetString());
        var route = $"/api/gates/{gate.GetProperty("id")}/decision";
        using var anon = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync(route, new { approved = true })).StatusCode);
        var decision = await Json(await client.PostAsJsonAsync(route, new { approved = true, note = "Reviewed" }));
        Assert.Equal("Approved", decision.GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty, decision.GetProperty("decidedBy").GetGuid());
        Assert.Equal("Reviewed", decision.GetProperty("note").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { approved = false })).StatusCode);
    }

    [Fact]
    public async Task BothRealSupervisorFormatsImportIdempotently()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "IMPORT");
        var route = $"/api/import/supervisor?projectId={project}";
        foreach (var fixture in new[] { "tasks.json", "queue.json" })
        {
            var input = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
            var counts = await Json(await client.PostAsync(route, new StringContent(input, Encoding.UTF8, "application/json")));
            Assert.True(counts.GetProperty("agentRuns").GetInt32() > 0);
            var runs = await ListCount(client, $"/api/agent-runs?projectId={project}");
            var evidence = await ListCount(client, $"/api/evidence?projectId={project}");
            var gates = await ListCount(client, $"/api/projects/{project}/gates");
            var reviews = await ListCount(client, $"/api/reviews?projectId={project}");
            Assert.True(gates > 0);
            await Json(await client.PostAsync(route, new StringContent(input, Encoding.UTF8, "application/json")));
            Assert.Equal(runs, await ListCount(client, $"/api/agent-runs?projectId={project}"));
            Assert.Equal(evidence, await ListCount(client, $"/api/evidence?projectId={project}"));
            Assert.Equal(gates, await ListCount(client, $"/api/projects/{project}/gates"));
            Assert.Equal(reviews, await ListCount(client, $"/api/reviews?projectId={project}"));
        }
    }

    [Fact]
    public async Task ImportPersistsStructuredEvidenceAndReviewButDoesNotOverruleHuman()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "EVIDENCE");
        var work = await NewWork(client, project, "EVIDENCE-1", "Worked item");
        var payload = new
        {
            tasks = new[] { new { id = "SYNTH-123", title = "Review", status = "WAITING_HUMAN", attempts = 2,
                implementer = "t1", workItemId = work,
                review = new { reviewer = "t2", verdict = "CHANGES_REQUESTED", findings = new[] { "Missing coverage" } },
                evidence = new[] { new { kind = "Test", title = "Suite", path = "test.log", result = "Pass" } } } }
        };
        var uri = $"/api/import/supervisor?projectId={project}";
        await Json(await client.PostAsJsonAsync(uri, payload));
        var gate = Assert.Single((await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray());
        await Json(await client.PostAsJsonAsync($"/api/gates/{gate.GetProperty("id")}/decision", new { approved = false }));
        await Json(await client.PostAsJsonAsync(uri, payload));
        var evidence = Assert.Single((await Json(await client.GetAsync($"/api/evidence?workItemId={work}"))).EnumerateArray());
        Assert.Equal("Pass", evidence.GetProperty("result").GetString());
        var review = Assert.Single((await Json(await client.GetAsync($"/api/reviews?projectId={project}&workItemId={work}"))).EnumerateArray());
        Assert.Equal("ChangesRequested", review.GetProperty("verdict").GetString());
        Assert.Single(review.GetProperty("findings").EnumerateArray());
        var latestGate = Assert.Single((await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray());
        Assert.Equal("Rejected", latestGate.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DashboardFiltersAndReportsReflectProjectData()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "REPORT");
        var sprint = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/sprints", SprintInput("Current")));
        var sprintId = sprint.GetProperty("id").GetGuid();
        var done = await NewWork(client, project, "REPORT-1", "Complete feature", sprintId, "Feature");
        var blocked = await NewWork(client, project, "REPORT-2", "Blocked test", sprintId);
        await Json(await PatchStatus(client, done, "Done"));
        await Json(await PatchStatus(client, blocked, "Blocked", "Test is pending"));
        await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates", new { title = "Approve release" }));
        var dashboard = await Json(await client.GetAsync($"/api/dashboard?projectId={project}"));
        Assert.Equal(1, dashboard.GetProperty("statusCounts").GetProperty("Done").GetInt32());
        Assert.Equal(50m, dashboard.GetProperty("activeSprintCompletionRate").GetDecimal());
        Assert.Single(dashboard.GetProperty("blockedItems").EnumerateArray());
        Assert.Single(dashboard.GetProperty("pendingGates").EnumerateArray());
        var filtered = await Json(await client.GetAsync($"/api/projects/{project}/work-items?status=Done&type=Feature&sprintId={sprintId}&q=feature&page=1&pageSize=1"));
        Assert.Equal(1, filtered.GetProperty("total").GetInt32());
        var report = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/reports", new { title = "Delivery" }));
        var id = report.GetProperty("id");
        var markdown = await client.GetStringAsync($"/api/reports/{id}/markdown");
        Assert.Contains("# Delivery", markdown);
        Assert.Contains("Test is pending", markdown);
        using var yaml = JsonDocument.Parse(await client.GetStringAsync($"/api/reports/{id}/stylus.yaml"));
        Assert.Equal(1, yaml.RootElement.GetProperty("spec_version").GetInt32());
        Assert.Equal("Delivery", yaml.RootElement.GetProperty("title").GetString());
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DRAUPNIR_TEST_STYLUS_EXECUTABLE")))
        {
            using var document = System.IO.Compression.ZipFile.OpenRead(report.GetProperty("docxPath").GetString()!);
            Assert.NotNull(document.GetEntry("word/document.xml"));
        }
        Assert.Equal(1, await ListCount(client, $"/api/projects/{project}/reports"));
    }

    private HttpClient NewClient() => postgres.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    private async Task<HttpClient> AuthorizedClient()
    {
        var client = NewClient();
        await Login(client);
        return client;
    }
    private static async Task<HttpResponseMessage> Login(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "ADMIN@example.test", password = "local-fixture-password" });
        response.EnsureSuccessStatusCode();
        return response;
    }
    private static async Task<Guid> NewProject(HttpClient client, string key) =>
        (await Json(await client.PostAsJsonAsync("/api/projects", new { key, name = key }))).GetProperty("id").GetGuid();
    private static async Task<Guid> NewWork(HttpClient client, Guid project, string code, string title, Guid? sprintId = null, string type = "Task") =>
        (await Json(await client.PostAsJsonAsync($"/api/projects/{project}/work-items", new { code, title, sprintId, type }))).GetProperty("id").GetGuid();
    private static Task<HttpResponseMessage> PatchStatus(HttpClient client, Guid id, string status, string? reason = null) =>
        client.PatchAsJsonAsync($"/api/work-items/{id}/status", new { status, reason });
    private static Task<HttpResponseMessage> AddDependency(HttpClient client, Guid id, Guid dependsOnId) =>
        client.PostAsJsonAsync($"/api/work-items/{id}/dependencies", new { dependsOnId });
    private static object SprintInput(string name) => new { name, startDate = "2026-11-24", endDate = "2026-11-30", status = "Active" };
    private static async Task<int> ListCount(HttpClient client, string route) => (await Json(await client.GetAsync(route))).GetArrayLength();

    private static async Task<JsonElement> Json(HttpResponseMessage response, bool requireSuccess = true)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (requireSuccess) Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }
}
