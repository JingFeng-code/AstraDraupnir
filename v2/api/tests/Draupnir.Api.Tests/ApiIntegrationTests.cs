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
        await Json(await admin.PostAsJsonAsync($"/api/gates/{gate.GetProperty("id")}/decision", new { approved = true }));

        var audit = await Json(await admin.GetAsync("/api/audit"));
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "auth.login");
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "membership.add");
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "user.create");
        Assert.Contains(audit.EnumerateArray(), row => row.GetProperty("action").GetString() == "gate.decision");

        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{memberId}", new { role = "Admin" })).StatusCode);
        Assert.Equal("Admin", (await Json(await member.GetAsync("/api/auth/me"))).GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{memberId}", new { role = "Member" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/projects/{allowed}/members/{memberId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/projects/{allowed}")).StatusCode);
        var afterRemove = await Json(await member.GetAsync("/api/projects"));
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
        await Json(await client.PostAsJsonAsync($"/api/gates/{gate.GetProperty("id")}/decision", new { approved = true }));
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

        // 成員可以改一般欄位
        var renamed = await member.PutAsJsonAsync($"/api/projects/{project}", new { key = "ARCH", name = "Renamed by member" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Renamed by member", (await Json(renamed)).GetProperty("name").GetString());

        // 但不能封存（Status 變更只有 Admin）
        var archive = await member.PutAsJsonAsync($"/api/projects/{project}", new { key = "ARCH", name = "Renamed by member", status = "Archived" });
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
        Assert.Equal("Active", (await Json(await admin.GetAsync($"/api/projects/{project}"))).GetProperty("status").GetString());

        // 也不能解除封存
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/projects/{project}", new { key = "ARCH", name = "Archived", status = "Archived" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync($"/api/projects/{project}", new { key = "ARCH", name = "Archived", status = "Active" })).StatusCode);
        Assert.Equal("Archived", (await Json(await admin.GetAsync($"/api/projects/{project}"))).GetProperty("status").GetString());

        // Admin 可以解除封存
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
        // 不論哪一個成功，系統必須還有至少一位有效 Admin（C 沒有被動到）
        using var verifier = NewClient();
        await LoginAs(verifier, emailC, passwordC);
        var users = await Json(await verifier.GetAsync("/api/users"));
        Assert.True(users.EnumerateArray().Count(u => u.GetProperty("role").GetString() == "Admin" && u.GetProperty("isActive").GetBoolean()) >= 1);

        // 還原種子管理員，避免影響同一測試資料庫中其他測試的登入。
        Assert.Equal(HttpStatusCode.OK, (await verifier.PatchAsJsonAsync($"/api/users/{idA}", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await verifier.PatchAsJsonAsync($"/api/users/{idB}", new { isActive = true })).StatusCode);
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
