using Npgsql;
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
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/projects")).StatusCode);
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
    public async Task BugsSupportSeverityFilterAndDependencyRules()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "BUGS");
        var bugA = await NewWork(client, project, "BUGS-1", "First bug", null, "Bug", "S2");
        var bugB = await NewWork(client, project, "BUGS-2", "Second bug", null, "Bug", "S1");

        var filtered = await Json(await client.GetAsync($"/api/projects/{project}/work-items?type=Bug"));
        Assert.Equal(2, filtered.GetProperty("total").GetInt32());
        var first = filtered.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "BUGS-1");
        Assert.Equal("Bug", first.GetProperty("type").GetString());
        Assert.Equal("S2", first.GetProperty("severity").GetString());

        var invalid = await client.PostAsJsonAsync($"/api/projects/{project}/work-items",
            new { code = "BUGS-3", title = "Bad severity", type = "Bug", severity = "S9" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await Json(invalid, requireSuccess: false)).GetProperty("errors").TryGetProperty("severity", out _));

        Assert.Equal(HttpStatusCode.Created, (await AddDependency(client, bugB, bugA)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PatchStatus(client, bugB, "Done")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatus(client, bugA, "Done")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatus(client, bugB, "Done")).StatusCode);
    }

    [Fact]
    public async Task SpoolResultsAreValidatedStoredAndIdempotent()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "SPOOL");
        var route = $"/api/projects/{project}/spool-results";
        var check = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "spool-check.json"));
        var impact = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "spool-impact.json"));

        var first = await Json(await client.PostAsync(route, new StringContent(check, Encoding.UTF8, "application/json")));
        Assert.True(first.GetProperty("imported").GetBoolean());
        var id = first.GetProperty("evidenceId").GetGuid();
        var duplicate = await Json(await client.PostAsync(route, new StringContent(check, Encoding.UTF8, "application/json")));
        Assert.False(duplicate.GetProperty("imported").GetBoolean());
        Assert.Equal(id, duplicate.GetProperty("evidenceId").GetGuid());

        var impactResult = await Json(await client.PostAsync(route, new StringContent(impact, Encoding.UTF8, "application/json")));
        Assert.True(impactResult.GetProperty("imported").GetBoolean());
        var stored = await Json(await client.GetAsync(route));
        Assert.Equal(2, stored.GetArrayLength());
        var checkRow = stored.EnumerateArray().Single(x => x.GetProperty("title").GetString() == "check");
        Assert.Equal("Trace", checkRow.GetProperty("kind").GetString());
        Assert.Equal("Pass", checkRow.GetProperty("result").GetString());
        var checkData = JsonDocument.Parse(checkRow.GetProperty("data").GetString()!).RootElement;
        Assert.Equal("pass", checkData.GetProperty("status").GetString());
        var impactRow = stored.EnumerateArray().Single(x => x.GetProperty("title").GetString() == "impact");
        var impactData = JsonDocument.Parse(impactRow.GetProperty("data").GetString()!).RootElement;
        Assert.Equal("sandbox-arithmetic", impactData.GetProperty("affectedTraces")[0].GetProperty("featureId").GetString());

        var evidence = await Json(await client.GetAsync($"/api/evidence?projectId={project}"));
        Assert.Contains(evidence.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        var audit = await Json(await client.GetAsync("/api/audit"));
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "spool.import");
        var invalid = await client.PostAsync(route,
            new StringContent("{\"schema\":\"spool-result/v1\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task MultiUserAuthorizationMatrixAndAudit()
    {
        using var admin = await AuthorizedClient();
        var allowed = await NewProject(admin, "MEMBER");
        var foreign = await NewProject(admin, "FOREIGN");
        var email = $"member-{Guid.NewGuid():N}@example.test";
        const string initialPassword = "member-pass-1";
        const string newPassword = "member-pass-2";
        var created = await Json(await admin.PostAsJsonAsync("/api/users",
            new { email, displayName = "Member One", password = initialPassword, role = "Member" }));
        Assert.Equal("Member", created.GetProperty("role").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());
        var memberId = created.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/projects/{allowed}/members",
            new { userId = memberId })).StatusCode);

        using var member = NewClient();
        await LoginAs(member, email, initialPassword);
        var listed = await Json(await member.GetAsync("/api/projects"));
        Assert.Contains(listed.EnumerateArray(), item => item.GetProperty("id").GetGuid() == allowed);
        Assert.DoesNotContain(listed.EnumerateArray(), item => item.GetProperty("id").GetGuid() == foreign);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/projects/{allowed}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/projects/{foreign}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync("/api/projects", new { key = "NOPE", name = "Nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/projects/{allowed}/members", new { userId = memberId })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await member.PostAsJsonAsync($"/api/projects/{allowed}/requirements",
            new { code = "REQ-1", title = "Member can edit", priority = "P2" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/projects/{foreign}/requirements",
            new { code = "REQ-1", title = "No access", priority = "P2" })).StatusCode);
        var gate = await Json(await admin.PostAsJsonAsync($"/api/projects/{allowed}/gates", new { title = "Member cannot approve" }));
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/gates/{gate.GetProperty("id")}/decision",
            new { approved = true })).StatusCode);
        await Json(await admin.PostAsJsonAsync($"/api/gates/{gate.GetProperty("id")}/decision", new { approved = true, note = "Reviewed by the local administrator." }));

        var audit = await Json(await admin.GetAsync("/api/audit"));
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "auth.login");
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "membership.add");
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "user.create");
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "gate.decision");

        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{memberId}", new { role = "Admin" })).StatusCode);
        // 角色變更會輪替 security stamp：舊 session 立即失效，必須重新登入才看得到新角色。
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/auth/me")).StatusCode);
        using var memberAsAdmin = NewClient();
        await LoginAs(memberAsAdmin, email, initialPassword);
        Assert.Equal("Admin", (await Json(await memberAsAdmin.GetAsync("/api/auth/me"))).GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{memberId}", new { role = "Member" })).StatusCode);
        using var memberAgain = NewClient();
        await LoginAs(memberAgain, email, initialPassword);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/projects/{allowed}/members/{memberId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberAgain.GetAsync($"/api/projects/{allowed}")).StatusCode);
        var afterRemove = await Json(await memberAgain.GetAsync("/api/projects"));
        Assert.DoesNotContain(afterRemove.EnumerateArray(), item => item.GetProperty("id").GetGuid() == allowed);

        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{memberId}", new { password = newPassword })).StatusCode);
        using var oldLogin = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldLogin.PostAsJsonAsync("/api/auth/login", new { email, password = initialPassword })).StatusCode);
        using var newLogin = NewClient();
        await LoginAs(newLogin, email, newPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await newLogin.GetAsync($"/api/projects/{allowed}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/projects/{allowed}/members", new { userId = memberId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await newLogin.GetAsync($"/api/projects/{allowed}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{memberId}", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await newLogin.GetAsync($"/api/projects/{allowed}")).StatusCode);
    }

    [Fact]
    public async Task AuditRecordsAuthAndCrudWithoutSecrets()
    {
        using var client = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@example.test", password = "wrong-password" })).StatusCode);
        await Login(client);
        var project = await NewProject(client, "AUDIT");
        var requirement = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/requirements",
            new { code = "REQ-1", title = "Audited requirement", priority = "P2" }));
        var work = await NewWork(client, project, "AUDIT-1", "Audited work");
        var other = await NewWork(client, project, "AUDIT-2", "Dependency");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/projects/{project}/requirements/{requirement.GetProperty("id")}",
            new { code = "REQ-1", title = "Edited requirement", priority = "P2" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/projects/{project}/work-items/{work}",
            new { code = "AUDIT-1", title = "Edited work", status = "Ready", priority = "P2" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/projects/{project}",
            new { key = "AUDIT", name = "Edited project", status = "Active" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatus(client, work, "InProgress")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AddDependency(client, work, other)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/work-items/{work}/dependencies/{other}")).StatusCode);
        var gate = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates", new { title = "Audited gate" }));
        await Json(await client.PostAsJsonAsync($"/api/gates/{gate.GetProperty("id")}/decision", new { approved = true, note = "Reviewed by the local administrator." }));
        await Json(await client.PostAsJsonAsync($"/api/projects/{project}/reports", new { title = "Audited report" }));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/projects/{project}")).StatusCode);

        var audit = await Json(await client.GetAsync("/api/audit"));
        var actions = audit.EnumerateArray().Select(row => row.GetProperty("action").GetString()).ToHashSet();
        foreach (var action in new[] { "auth.login", "auth.login.failed", "project.create", "requirement.create",
                     "work_item.create", "work_item.update", "work_item.status", "requirement.update",
                     "dependency.add", "dependency.remove", "gate.create", "gate.decision", "report.generate",
                     "project.update", "project.archive" })
            Assert.Contains(action, actions);
        foreach (var row in audit.EnumerateArray())
            Assert.DoesNotContain("local-fixture-password", row.GetProperty("detail").GetString() ?? "");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        await Login(client);
        var afterLogout = await Json(await client.GetAsync("/api/audit"));
        Assert.Contains(afterLogout.EnumerateArray(), row => row.GetProperty("action").GetString() == "auth.logout");
    }

    [Fact]
    public async Task F03SameTaskIdInTwoSourceInstancesStaysSeparate()
    {
        using var client = await AuthorizedClient();
        var first = await NewProject(client, "SRCA");
        var second = await NewProject(client, "SRCB");
        var payload = new
        {
            tasks = new[] { new { id = "SHARED-TASK", title = "Same id", status = "DONE", repo = "sandbox",
                implementer = "t1", attempt = 1 } }
        };
        var one = await client.PostAsJsonAsync($"/api/import/supervisor?projectId={first}&sourceInstance=one", payload);
        var two = await client.PostAsJsonAsync($"/api/import/supervisor?projectId={second}&sourceInstance=two", payload);
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal(HttpStatusCode.OK, two.StatusCode);
        Assert.Equal(1, await ListCount(client, $"/api/agent-runs?projectId={first}"));
        Assert.Equal(1, await ListCount(client, $"/api/agent-runs?projectId={second}"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/import/supervisor?projectId={first}&sourceInstance=one", payload)).StatusCode);
        Assert.Equal(1, await ListCount(client, $"/api/agent-runs?projectId={first}"));
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
        var decision = await Json(await client.PostAsJsonAsync(route, new { approved = true, note = "Reviewed by the local administrator." }));
        Assert.Equal("Approved", decision.GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty, decision.GetProperty("decidedBy").GetGuid());
        Assert.Equal("Reviewed by the local administrator.", decision.GetProperty("note").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { approved = false })).StatusCode);
    }

    [Fact]
    public async Task GateOnAWorkItemCannotBeApprovedWithoutEvidenceOrAnException()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "GATEEV");
        var item = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/work-items",
            new { code = "GATEEV-1", title = "Gate target", type = "Task" }));
        var workItemId = item.GetProperty("id").GetGuid();
        var gate = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
            new { title = "Needs evidence", workItemId, externalRef = "test:GATEEV-1" }));
        var route = $"/api/gates/{gate.GetProperty("id")}/decision";

        // 直接核准必須被後端擋下（前端規則不能是唯一防線）
        var blocked = await client.PostAsJsonAsync(route, new { approved = true, note = "ok" });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.True((await Json(blocked, requireSuccess: false)).GetProperty("errors").TryGetProperty("note", out _));

        // 駁回不需要證據
        var rejected = await Json(await client.PostAsJsonAsync(route, new { approved = false, note = "no evidence" }));
        Assert.Equal("Rejected", rejected.GetProperty("status").GetString());

        // 同一個工作項目的第二個 Gate：人工例外（前綴 + 20 字）可以核准
        var second = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
            new { title = "Needs exception", workItemId, externalRef = "test:GATEEV-2" }));
        var reason = "測試環境沒有真實證據，由管理者承擔風險後放行";
        var approved = await Json(await client.PostAsJsonAsync($"/api/gates/{second.GetProperty("id")}/decision",
            new { approved = true, note = $"{GateRules.ExceptionPrefix}{reason}" }));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.StartsWith(GateRules.ExceptionPrefix, approved.GetProperty("note").GetString());
    }

    [Fact]
    public async Task GateRejectsMissingFailChangesAndNonPassEvidence()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "GATEAA");

        async Task<Guid> Item(string suffix) =>
            await NewWork(client, project, $"GATEAA-{suffix}", $"Gate target {suffix}");

        async Task<Guid> Gate(Guid item, string suffix) =>
            (await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
                new { title = $"Gate {suffix}", workItemId = item, externalRef = $"test:F04-{suffix}" }))).GetProperty("id").GetGuid();

        // missing evidence
        var missing = await Item("1");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/gates/{await Gate(missing, "missing")}/decision", new { approved = true })).StatusCode);

        // Fail
        var failed = await Item("2");
        await ImportEvidence(client, project, failed, "F04-FAIL", 1, "Fail");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/gates/{await Gate(failed, "fail")}/decision", new { approved = true })).StatusCode);

        // ChangesRequested
        var changed = await Item("3");
        await ImportEvidence(client, project, changed, "F04-CHANGES", 1, null, verdict: "ChangesRequested");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/gates/{await Gate(changed, "changes")}/decision", new { approved = true })).StatusCode);

        // NotTested only
        var notTested = await Item("4");
        await ImportEvidence(client, project, notTested, "F04-NOTTESTED", 1, "NotTested");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/gates/{await Gate(notTested, "nottested")}/decision", new { approved = true })).StatusCode);

        // Blocked only
        var blocked = await Item("5");
        await ImportEvidence(client, project, blocked, "F04-BLOCKED", 1, "Blocked");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/gates/{await Gate(blocked, "blocked")}/decision", new { approved = true })).StatusCode);
    }

    [Fact]
    public async Task GateUsesOnlyTheLatestAttemptAndFreshEvidence()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "GATEAB");

        var retried = await NewWork(client, project, "GATEAB-1", "Retried");
        await ImportEvidence(client, project, retried, "F04-RETRY", 1, "Fail",
            DateTimeOffset.UtcNow.AddHours(-2));
        // The old Fail is stale after this update; the newer attempt still supplies a fresh Pass.
        await client.PatchAsJsonAsync($"/api/work-items/{retried}/status", new { status = "InProgress" });
        await ImportEvidence(client, project, retried, "F04-RETRY", 2, "Pass");
        var retriedGate = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
            new { title = "Retried gate", workItemId = retried, externalRef = "test:F04-RETRY" }));
        var approved = await Json(await client.PostAsJsonAsync($"/api/gates/{retriedGate.GetProperty("id")}/decision",
            new { approved = true }));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        var snapshot = approved.GetProperty("evidenceSnapshot");
        Assert.False(snapshot.GetProperty("exceptionUsed").GetBoolean());
        Assert.Equal(1, snapshot.GetProperty("counts").GetProperty("Pass").GetInt32());
        Assert.Equal(0, snapshot.GetProperty("counts").GetProperty("Fail").GetInt32());

        // A newer work-item update makes the old pass stale; it cannot be the sole basis.
        var stale = await NewWork(client, project, "GATEAB-2", "Stale");
        await ImportEvidence(client, project, stale, "F04-STALE", 1, "Pass",
            DateTimeOffset.UtcNow.AddHours(-1));
        await client.PatchAsJsonAsync($"/api/work-items/{stale}/status", new { status = "InProgress" });
        var staleGate = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
            new { title = "Stale gate", workItemId = stale, externalRef = "test:F04-STALE" }));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/gates/{staleGate.GetProperty("id")}/decision", new { approved = true })).StatusCode);
    }

    [Fact]
    public async Task GateManualExceptionAndNoWorkItemReasonAreRecordedInSnapshotAndAudit()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "GATEAC");
        var item = await NewWork(client, project, "GATEAC-1", "Exception");
        await ImportEvidence(client, project, item, "F04-EXCEPTION", 1, "Fail");
        var exceptionGate = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
            new { title = "Exception gate", workItemId = item, externalRef = "test:F04-EXCEPTION" }));
        var reason = "測試環境沒有真實證據，由管理者承擔風險後放行";
        var exception = await Json(await client.PostAsJsonAsync($"/api/gates/{exceptionGate.GetProperty("id")}/decision",
            new { approved = true, note = $"{GateRules.ExceptionPrefix}{reason}" }));
        Assert.True(exception.GetProperty("evidenceSnapshot").GetProperty("exceptionUsed").GetBoolean());

        var empty = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
            new { title = "Manual gate", externalRef = "test:F04-MANUAL" }));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/gates/{empty.GetProperty("id")}/decision", new { approved = true })).StatusCode);
        var manual = await Json(await client.PostAsJsonAsync($"/api/gates/{empty.GetProperty("id")}/decision",
            new { approved = true, note = "這是沒有工作項目的人工判斷理由，至少二十字。" }));
        Assert.Equal("Approved", manual.GetProperty("status").GetString());
        Assert.Equal("這是沒有工作項目的人工判斷理由，至少二十字。",
            manual.GetProperty("evidenceSnapshot").GetProperty("manualReason").GetString());
        var audit = await Json(await client.GetAsync("/api/audit"));
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "gate.decision" &&
            row.GetProperty("detail").GetString()!.Contains("reason=這是沒有工作項目的人工判斷理由"));
    }

    [Fact]
    public async Task ProjectStatusCanOnlyBeChangedByAnAdmin()
    {
        using var admin = await AuthorizedClient();
        var project = await NewProject(admin, "ARCH");
        var email = "arch-member@example.test";
        var password = "Arch-member-pass-1";
        var memberId = (await Json(await admin.PostAsJsonAsync("/api/users",
            new { email, displayName = "Arch member", password, role = "Member" }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/projects/{project}/members", new { userId = memberId })).StatusCode);
        using var member = NewClient();
        await LoginAs(member, email, password);

        var renamed = await member.PutAsJsonAsync($"/api/projects/{project}", new { key = "ARCH", name = "Renamed by member" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Renamed by member", (await Json(renamed)).GetProperty("name").GetString());

        var archive = await member.PutAsJsonAsync($"/api/projects/{project}", new { key = "ARCH", name = "Renamed by member", status = "Archived" });
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
        Assert.Equal("Active", (await Json(await admin.GetAsync($"/api/projects/{project}"))).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/projects/{project}", new { key = "ARCH", name = "Archived", status = "Archived" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync($"/api/projects/{project}", new { key = "ARCH", name = "Archived", status = "Active" })).StatusCode);
        Assert.Equal("Archived", (await Json(await admin.GetAsync($"/api/projects/{project}"))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/projects/{project}", new { key = "ARCH", name = "Archived", status = "Active" })).StatusCode);
        Assert.Equal("Active", (await Json(await admin.GetAsync($"/api/projects/{project}"))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ConcurrentDemotionsCannotRemoveTheLastActiveAdministrator()
    {
        using var adminA = await AuthorizedClient();
        var idA = (await Json(await adminA.GetAsync("/api/auth/me"))).GetProperty("id").GetGuid();
        var emailB = "admin-b@example.test";
        var passwordB = "Admin-b-pass-1";
        var idB = (await Json(await adminA.PostAsJsonAsync("/api/users",
            new { email = emailB, displayName = "Admin B", password = passwordB, role = "Admin" }))).GetProperty("id").GetGuid();
        var emailC = "admin-c@example.test";
        var passwordC = "Admin-c-pass-1";
        await Json(await adminA.PostAsJsonAsync("/api/users", new { email = emailC, displayName = "Admin C", password = passwordC, role = "Admin" }));
        using var adminB = NewClient();
        await LoginAs(adminB, emailB, passwordB);

        using var barrier = new Barrier(2);
        async Task<HttpStatusCode> Deactivate(HttpClient client, Guid target)
        {
            barrier.SignalAndWait();
            var response = await client.PatchAsJsonAsync($"/api/users/{target}", new { isActive = false });
            return response.StatusCode;
        }

        var first = Task.Run(() => Deactivate(adminA, idB));
        var second = Task.Run(() => Deactivate(adminB, idA));
        var codes = await Task.WhenAll(first, second);

        Assert.Contains(codes, code => code == HttpStatusCode.OK);
        Assert.Contains(codes, code => code != HttpStatusCode.OK);
        using var verifier = NewClient();
        await LoginAs(verifier, emailC, passwordC);
        var users = await Json(await verifier.GetAsync("/api/users"));
        Assert.True(users.EnumerateArray().Count(u => u.GetProperty("role").GetString() == "Admin" && u.GetProperty("isActive").GetBoolean()) >= 1);

        // 還原種子管理員，避免影響同一測試資料庫中其他測試的登入。
        Assert.Equal(HttpStatusCode.OK, (await verifier.PatchAsJsonAsync($"/api/users/{idA}", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await verifier.PatchAsJsonAsync($"/api/users/{idB}", new { isActive = true })).StatusCode);
    }

    [Fact]
    public async Task PasswordResetAndRoleChangeInvalidateExistingSessions()
    {
        using var admin = await AuthorizedClient();
        var email = "session-user@example.test";
        var password = "Session-pass-1";
        var userId = (await Json(await admin.PostAsJsonAsync("/api/users",
            new { email, displayName = "Session user", password, role = "Member" }))).GetProperty("id").GetGuid();
        using var member = NewClient();
        await LoginAs(member, email, password);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{userId}", new { password = "Session-pass-2" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/auth/me")).StatusCode);

        using var relogin = NewClient();
        await LoginAs(relogin, email, "Session-pass-2");
        Assert.Equal(HttpStatusCode.OK, (await relogin.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{userId}", new { role = "Admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await relogin.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task RepeatedFailedLoginsAreThrottled()
    {
        using var client = NewClient();
        var email = "throttle@example.test";
        for (var attempt = 1; attempt <= 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new { email, password = "wrong-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "wrong-password" })).StatusCode);
    }

    [Fact]
    public async Task GatesAreSeparatePerSourceInstanceAndKeepTheirDecision()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "SRCGATE");
        var payload = new
        {
            tasks = new[] { new { id = "TASK-001", title = "Source gate", status = "WAITING_HUMAN",
                requiresHuman = true, repo = "sandbox", implementer = "t1" } }
        };
        await Json(await client.PostAsJsonAsync($"/api/import/supervisor?projectId={project}&sourceInstance=one", payload));
        await Json(await client.PostAsJsonAsync($"/api/import/supervisor?projectId={project}&sourceInstance=two", payload));
        var gates = (await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray().ToList();
        Assert.Equal(2, gates.Count);
        var one = gates.Single(g => g.GetProperty("externalRef").GetString()!.Contains(":one:"));
        var two = gates.Single(g => g.GetProperty("externalRef").GetString()!.Contains(":two:"));

        // A 核准不影響 B
        await Json(await client.PostAsJsonAsync($"/api/gates/{one.GetProperty("id")}/decision",
            new { approved = true, note = "Source one reviewed by a human" }));
        var afterApprove = (await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray().ToList();
        Assert.Equal("Approved", afterApprove.Single(g => g.GetProperty("id").GetGuid() == one.GetProperty("id").GetGuid()).GetProperty("status").GetString());
        Assert.Equal("Pending", afterApprove.Single(g => g.GetProperty("id").GetGuid() == two.GetProperty("id").GetGuid()).GetProperty("status").GetString());

        // 同來源重匯仍冪等，且不覆蓋已決定的 Gate
        await Json(await client.PostAsJsonAsync($"/api/import/supervisor?projectId={project}&sourceInstance=one", payload));
        var afterReimport = (await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray().ToList();
        Assert.Equal(2, afterReimport.Count);
        Assert.Equal("Approved", afterReimport.Single(g => g.GetProperty("id").GetGuid() == one.GetProperty("id").GetGuid()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task LegacyGateWithoutSourceInstanceIsAdoptedNotDuplicated()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "LEGACY");
        // 升級前格式的 Gate（沒有來源實例）
        var legacy = await Json(await client.PostAsJsonAsync($"/api/projects/{project}/gates",
            new { title = "Legacy gate", externalRef = "supervisor:LEG-1" }));
        var payload = new
        {
            tasks = new[] { new { id = "LEG-1", title = "Legacy gate", status = "WAITING_HUMAN",
                requiresHuman = true, repo = "sandbox", implementer = "t1" } }
        };
        await Json(await client.PostAsJsonAsync($"/api/import/supervisor?projectId={project}", payload));
        var gates = (await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray().ToList();
        Assert.Single(gates);
        Assert.Equal(legacy.GetProperty("id").GetGuid(), gates[0].GetProperty("id").GetGuid());
        Assert.Contains(":LEGACY:", gates[0].GetProperty("externalRef").GetString());
    }

    [Fact]
    public async Task ProjectRenameDoesNotChangeTheImportNamespace()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "RENAME");
        var payload = new
        {
            tasks = new[] { new { id = "TASK-9", title = "Rename gate", status = "WAITING_HUMAN",
                requiresHuman = true, repo = "sandbox", implementer = "t1" } }
        };
        await Json(await client.PostAsJsonAsync($"/api/import/supervisor?projectId={project}", payload));
        var first = (await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray().Single();
        // 沒有工作項目時可以改 Key
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/projects/{project}",
            new { key = "RENAMED", name = "Renamed", status = "Active" })).StatusCode);
        await Json(await client.PostAsJsonAsync($"/api/import/supervisor?projectId={project}", payload));
        var gates = (await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray().ToList();
        Assert.Single(gates);
        Assert.Equal(first.GetProperty("id").GetGuid(), gates[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task DecisionSnapshotDoesNotChangeWhenTheEvidenceIsReimported()
    {
        using var client = await AuthorizedClient();
        var project = await NewProject(client, "SNAP");
        var work = await NewWork(client, project, "SNAP-1", "Snapshot item");
        var passPayload = new
        {
            tasks = new[] { new { id = "SNAP-TASK", title = "Snapshot gate", status = "REVIEW_READY",
                implementer = "t1", workItemId = work,
                evidence = new[] { new { kind = "Test", title = "Suite", path = "test.log", result = "Pass" } },
                requiresHuman = true } }
        };
        var uri = $"/api/import/supervisor?projectId={project}";
        await Json(await client.PostAsJsonAsync(uri, passPayload));
        var gate = (await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray().Single();
        var decided = await Json(await client.PostAsJsonAsync($"/api/gates/{gate.GetProperty("id")}/decision",
            new { approved = true, note = "Evidence checked by a human" }));
        var snapshot = decided.GetProperty("evidenceSnapshot");
        Assert.Contains("contentHash", snapshot.GetProperty("usedEvidence").GetRawText());
        var canonical = CanonicalJson(snapshot);

        // 重新匯入並把同一筆證據改成 Fail：歷史決策快照不得變動
        var failPayload = new
        {
            tasks = new[] { new { id = "SNAP-TASK", title = "Snapshot gate", status = "REVIEW_READY",
                implementer = "t1", workItemId = work,
                evidence = new[] { new { kind = "Test", title = "Suite", path = "test.log", result = "Fail" } },
                requiresHuman = true } }
        };
        await Json(await client.PostAsJsonAsync(uri, failPayload));
        var after = (await Json(await client.GetAsync($"/api/projects/{project}/gates"))).EnumerateArray().Single();
        Assert.Equal(canonical, CanonicalJson(after.GetProperty("evidenceSnapshot")));
        Assert.Equal("Approved", after.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ExactlyTwoAdminsStayAtOneUnderConcurrentDemotions()
    {
        using var adminA = await AuthorizedClient();
        var emailB = "only-b@example.test";
        var passwordB = "Only-b-pass-1";
        var idB = (await Json(await adminA.PostAsJsonAsync("/api/users",
            new { email = emailB, displayName = "Only B", password = passwordB, role = "Admin" }))).GetProperty("id").GetGuid();
        var idA = (await Json(await adminA.GetAsync("/api/auth/me"))).GetProperty("id").GetGuid();
        // 讓 A、B 成為僅有的兩位有效 Admin
        var users = (await Json(await adminA.GetAsync("/api/users"))).EnumerateArray()
            .Where(u => u.GetProperty("role").GetString() == "Admin").ToList();
        foreach (var other in users.Where(u => u.GetProperty("id").GetGuid() != idA && u.GetProperty("id").GetGuid() != idB))
            Assert.Equal(HttpStatusCode.OK, (await adminA.PatchAsJsonAsync($"/api/users/{other.GetProperty("id").GetGuid()}",
                new { isActive = false })).StatusCode);
        using var adminB = NewClient();
        await LoginAs(adminB, emailB, passwordB);

        using var barrier = new Barrier(2);
        async Task<HttpStatusCode> Deactivate(HttpClient client, Guid target)
        {
            barrier.SignalAndWait();
            return (await client.PatchAsJsonAsync($"/api/users/{target}", new { isActive = false })).StatusCode;
        }

        var codes = await Task.WhenAll(Task.Run(() => Deactivate(adminA, idB)), Task.Run(() => Deactivate(adminB, idA)));
        Assert.Contains(codes, code => code == HttpStatusCode.OK);
        Assert.Contains(codes, code => code != HttpStatusCode.OK);

        // 直接查測試資料庫，確認 invariant 真的成立（不靠 API）
        await using var connection = new Npgsql.NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using (var command = new Npgsql.NpgsqlCommand(
            "SELECT count(*) FROM \"Users\" WHERE \"Role\" = 0 AND \"IsActive\" = true", connection))
        {
            var active = Convert.ToInt64(await command.ExecuteScalarAsync());
            Assert.True(active >= 1, $"active admins = {active}");
        }

        // 還原 A、B 以免影響同一資料庫的其他測試
        await using (var restore = new Npgsql.NpgsqlCommand(
            "UPDATE \"Users\" SET \"IsActive\" = true, \"SecurityStamp\" = gen_random_uuid() WHERE \"Id\" = ANY(@ids)",
            connection))
        {
            restore.Parameters.AddWithValue("ids", new[] { idA, idB });
            await restore.ExecuteNonQueryAsync();
        }
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
            var audit = await Json(await client.GetAsync("/api/audit"));
            Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "supervisor.import");
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
        var audit = await Json(await client.GetAsync("/api/audit"));
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "report.generate");
    }

    private static async Task ImportEvidence(HttpClient client, Guid project, Guid workItem, string taskId,
        int attempt, string? result, DateTimeOffset? recordedAt = null, string? verdict = null)
    {
        var evidence = result is null ? Array.Empty<object>() : new object[]
        {
            new { id = $"{taskId}-evidence", kind = "Test", title = $"{taskId} evidence", path = "test.log",
                result, recordedAt = (recordedAt ?? DateTimeOffset.UtcNow).ToString("O") }
        };
        var payload = new
        {
            tasks = new[]
            {
                new { id = taskId, status = "DONE", repo = "sandbox", implementer = "t1", attempt, workItemId = workItem,
                    evidence, review = verdict is null ? null : new { verdict } }
            }
        };
        var response = await client.PostAsJsonAsync($"/api/import/supervisor?projectId={project}&sourceInstance=gate-f04", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>把 JSON 依 key 排序後再比較：DB 讀回的快照 key 順序與回應不同，但內容必須一致。</summary>
    private static string CanonicalJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().OrderBy(property => property.Name)
            .Select(property => $"\"{property.Name}\":{CanonicalJson(property.Value)}")) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(CanonicalJson)) + "]",
        _ => element.GetRawText()
    };

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

    private static async Task LoginAs(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
    }
    private static async Task<Guid> NewProject(HttpClient client, string key) =>
        (await Json(await client.PostAsJsonAsync("/api/projects", new { key, name = key }))).GetProperty("id").GetGuid();
    private static async Task<Guid> NewWork(HttpClient client, Guid project, string code, string title, Guid? sprintId = null, string type = "Task", string? severity = null) =>
        (await Json(await client.PostAsJsonAsync($"/api/projects/{project}/work-items", new { code, title, sprintId, type, severity }))).GetProperty("id").GetGuid();
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
