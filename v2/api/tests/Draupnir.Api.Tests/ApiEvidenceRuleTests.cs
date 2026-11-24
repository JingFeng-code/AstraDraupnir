using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Draupnir.Api.Tests;

/// <summary>
/// R2-02: latest-attempt grouping must be per source task, not per source only.
/// Two tasks from the same Supervisor source are independent evidence streams; a Fail on
/// TASK-A must not disappear when TASK-B happens to have the newest run row.
/// </summary>
public sealed class ApiEvidenceRuleTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SameSourceDifferentTasksWithAFailAndPassBlocksApproval()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client);
        var item = await NewWork(client, project);
        await Import(client, project, item, sourceInstance: "r2-task-group",
            Task(item, "TASK-A", attempt: 1, result: "Fail", startedAt: "2026-11-24T00:00:00Z"),
            Task(item, "TASK-B", attempt: 2, result: "Pass", startedAt: "2026-11-24T02:00:00Z"));
        var gate = await NewGate(client, project, item, "r2-task-group-mixed");
        var decision = await client.PostAsJsonAsync($"/api/gates/{gate}/decision", new { approved = true });
        Assert.Equal(HttpStatusCode.BadRequest, decision.StatusCode);
    }

    [Fact]
    public async Task SameSourceTaskRetriedToPassAllowsApproval()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "EVIDAB");
        var item = await NewWork(client, project, "EVIDAB-1");
        await Import(client, project, item, sourceInstance: "r2-task-retry",
            Task(item, "TASK-A", attempt: 1, result: "Fail", startedAt: "2026-11-24T00:00:00Z"),
            Task(item, "TASK-B", attempt: 1, result: "Pass", startedAt: "2026-11-24T01:00:00Z"),
            Task(item, "TASK-A", attempt: 2, result: "Pass", startedAt: "2026-11-24T02:00:00Z"));
        var gate = await NewGate(client, project, item, "r2-task-retry");
        var decision = await client.PostAsJsonAsync($"/api/gates/{gate}/decision", new { approved = true });
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
    }

    private HttpClient NewClient() =>
        postgres.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private async Task<HttpClient> AuthorizedClient()
    {
        var client = NewClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@example.test", password = "local-fixture-password" });
        login.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<Guid> NewProject(HttpClient client, string key = "EVIDAA") =>
        (await Json(await client.PostAsJsonAsync("/api/projects", new { key, name = key })))
        .GetProperty("id").GetGuid();

    private static async Task<Guid> NewWork(HttpClient client, Guid project, string code = "EVIDAA-1") =>
        (await Json(await client.PostAsJsonAsync($"/api/projects/{project}/work-items",
            new { code, title = "R2 evidence target", type = "Task" })))
        .GetProperty("id").GetGuid();

    private static async Task<Guid> NewGate(HttpClient client, Guid project, Guid item, string externalRef) =>
        (await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
            new { title = "R2 evidence gate", workItemId = item, externalRef })))
        .GetProperty("id").GetGuid();

    private static object Task(Guid item, string taskId, int attempt, string result, string startedAt) => new
    {
        id = taskId,
        status = "DONE",
        repo = "sandbox",
        implementer = "t1",
        attempt,
        workItemId = item,
        startedAt,
        evidence = new[]
        {
            new
            {
                id = $"{taskId}-{attempt}-evidence",
                kind = "Test",
                title = $"{taskId} evidence",
                path = "test.log",
                result,
                recordedAt = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O")
            }
        }
    };

    private static async Task Import(HttpClient client, Guid project, Guid item, string sourceInstance,
        params object[] tasks)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/import/supervisor?projectId={project}&sourceInstance={sourceInstance}",
            new { tasks });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response, bool requireSuccess = true)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (requireSuccess) Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }
}
