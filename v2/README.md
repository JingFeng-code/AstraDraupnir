# AstraDraupnir V2

The V2 API runs locally with .NET SDK 10, EF Core 10, and PostgreSQL 18. The original Angular app remains at the repository root. The V2 web app is a separate deliverable.

## Start PostgreSQL and migrate

Use any local PostgreSQL 16+ instance (the delivery machine uses a portable PostgreSQL 18 on `127.0.0.1:55432` with local trust authentication). Start it with your usual tooling, then:

~~~powershell
cd v2/api
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet build --no-restore
dotnet ef database update --project src/Draupnir.Api --no-build
~~~

The commands use the already restored packages and local dotnet-ef tool in this workspace. A fresh environment needs the dependencies in Draupnir.Api.csproj and the tool in dotnet-tools.json provisioned before using --no-restore. The development database is draupnir. Set ConnectionStrings__Draupnir to select another database.

## Administrator, demo data, API

Set DRAUPNIR_ADMIN_EMAIL and DRAUPNIR_ADMIN_PASSWORD in the API process environment before first startup. Use an email and password chosen for this local instance. With either value missing, startup logs a warning and skips enrollment. Once an administrator exists, startup preserves the existing account and password. The password is hashed with ASP.NET Core Identity.

~~~powershell
# Optional synthetic demo project, requirements, sprint, item, and gate.
dotnet run --project src/Draupnir.Api --no-build --no-restore -- --seed-demo

# The default HTTP profile serves the local API on http://localhost:5287.
dotnet run --project src/Draupnir.Api --no-build --no-restore
~~~

The OpenAPI document is /openapi/v1.json. Sign in with a JSON email and password at POST /api/auth/login. Keep the returned session cookie on subsequent /api requests. GET /api/auth/me reads the current account and POST /api/auth/logout clears the cookie. Gate decisions at POST /api/gates/{id}/decision require that account. Agent and Supervisor data arrive through import.

### Users and project membership

Draupnir now has local accounts with `Admin` and `Member` roles. The seeded administrator is Admin. Admin can list/create/disable users and reset passwords through `GET/POST /api/users` and `PATCH /api/users/{id}`; the UI is `/users`. Admin adds or removes project members through `GET/POST /api/projects/{id}/members` and `DELETE /api/projects/{id}/members/{userId}`; the project overview shows the members panel to Admin.

A Member can see and edit only projects they belong to. A foreign project is `403` (not `404`), and `/api/projects` excludes it from the list. Members cannot create/archive projects, manage users or members, or decide a Human Gate. The gate buttons are hidden for Members, and the API also enforces Admin-only decisions. User, membership, and gate changes write an `AuditLog` row.

SSO/OIDC is intentionally out of scope and blocked on an external identity provider; see `adr/ADR-006-draupnir-multi-user.md`.

### Security policy: local development vs. production

The delivery runs on `127.0.0.1` with a local PostgreSQL and a local administrator account. That is the threat model it was tested against. Before any deployment:

| Control | Local development (this delivery) | Before production |
|---|---|---|
| Transport | plain HTTP on loopback | TLS everywhere; set `options.Cookie.SecurePolicy = Always` (the code keeps `SameAsRequest` so localhost works) |
| Session lifetime | 8 hours absolute (`ExpireTimeSpan`, `SlidingExpiration = false`) | same, or shorter; align with the organisation's policy |
| Session revocation | `User.SecurityStamp` is a cookie claim, rotated on every user change (password, role, active flag); `Access.Current` and `/api/auth/me` reject a stale or missing stamp | same; add a "sign out everywhere" action if needed |
| Login throttling | 5 failed attempts per e-mail inside 10 minutes -> `429` (counted from audit log; correct credentials are also temporarily throttled after the threshold) | keep, and add an IP-based limiter at the edge |
| Secrets | administrator password only via `DRAUPNIR_ADMIN_EMAIL` / `DRAUPNIR_ADMIN_PASSWORD`; nothing is committed; E2E needs `E2E_ADMIN_PASSWORD` | use a secret manager, rotate the bootstrap password, and remove the seed path once the first admin exists |
| API schema | `/openapi/v1.json` is anonymous (handy locally) | disable it or put it behind authorization |
| Roles | `Admin` / `Member` only; no MFA, no SSO/OIDC (ADR-006) | add SSO/MFA through an external identity provider |

The local preview databases (`draupnir`, `draupnir_design`, `draupnir_v21`) are **not disposable E2E targets**; preserve their existing data; the administrator passwords for them were rotated at delivery time and are not published in this repository.

### Spool results

`POST /api/projects/{id}/spool-results` accepts a `spool-result/v1` JSON body (`schema`, `command`, `status`, `exitCode`, `findings`, `counts`, optional `affectedTraces`). It stores a `Trace` evidence row named after the command, with the original JSON in `data`; `status=pass` maps to `Pass`, `status=fail` to `Fail`. A SHA-256 of the payload makes re-import idempotent. `GET /api/projects/{id}/spool-results` and the Evidence page show the command, status, counts and affected traces. Invalid schema/status/arrays are `400`.

POST /api/import/supervisor is Admin-only and accepts Supervisor queue.json or delivery state/tasks.json. Prefer explicit ?projectId= and ?sourceInstance= for trusted imports. Identity is Source + SourceInstance + ExternalTaskId + Attempt. Source precedence: explicit query, payload-level sourceInstance, then the selected project's frozen SourceInstance (first import sets it; migration backfills project Key). Without both a selected project and explicit source, the API can reuse a unique existing task/attempt; multiple matches return 409. When no run exists it matches or creates a project using repository information, then uses the project's namespace. Re-import of the same identity updates its run. Cross-project association conflicts are refused. Default and custom Gate refs are namespaced; legacy Pending gates may be adopted, decided legacy gates remain and a new Pending gate is created. A crafted custom ref may still adopt another source's Pending gate (L-01 in ../docs/v21/BACKLOG.md); do not import untrusted snapshots. A reviewer assignment alone does not produce a verdict.

GET /api/projects/{id}/work-items accepts status, type, sprintId, q, page, and pageSize. It returns items, total, page, and pageSize. DELETE /api/projects/{id} archives the project. The dashboard activeSprintCompletionRate is a percentage from 0 to 100.

POST /api/projects/{id}/reports creates a Markdown report and Stylus DocumentSpec. Fetch them from /api/reports/{id}/markdown and /api/reports/{id}/stylus.yaml. The spec uses JSON syntax, which Stylus accepts as YAML. If the native Stylus executable is on PATH or configured by DRAUPNIR_STYLUS_EXECUTABLE, the API also attempts to render DOCX. Set DRAUPNIR_REPORT_DIR to control where the generated files are written. The default is reports/ under the API process working directory. Markdown and YAML remain available when Stylus is unavailable.

### Human Gate evidence rules

The API is the enforcement point; the Web rules are a readable mirror. A gate linked to a work item evaluates only the **latest attempt of each source task** (`Source` + `SourceInstance` + `ExternalTaskId`) and only **fresh** evidence: an evidence row recorded before the work item's `UpdatedAt` is stale and cannot be the sole basis for approval. `Pass` is required; `Fail` and `ChangesRequested` block direct approval; `Blocked` and `NotTested` are not passes. Older attempts, stale rows and their IDs remain visible in the trace and are listed in the gate snapshot, but do not block a later successful retry. A SERIALIZABLE decision transaction stores `EvidenceSnapshot` (counts, used/latest/stale IDs, latest attempts, evaluated evidence metadata/hash, review verdict and exception/manual reason) so it remains auditable after evidence changes. The hash describes stored evidence data or metadata; it does not independently verify the bytes of an external file.

A linked gate with missing/failed evidence can only be approved with the fixed manual-exception prefix plus a reason of at least 20 characters; it remains Admin-only. A gate with **no work item** is a human judgement, but approval now requires a written reason of at least 20 characters and the reason is written to the `gate.decision` audit row.

## Tests

~~~powershell
cd v2/api
dotnet build --no-restore
dotnet test --no-build --no-restore
~~~

Integration tests use WebApplicationFactory and PostgreSQL. The test connection defaults to the local postgres maintenance database at port 55432. DRAUPNIR_TEST_POSTGRES overrides it. The integration test fixture creates, migrates, and removes its own draupnir_test_<guid> database. Final local Agent regression report for cdf0ee1: API 49/49, Web 52/52, Playwright 26/26 (desktop 1280 and mobile 360px; delivery flow, V2.1 acceptance and keyboard/a11y); the E2E includes Bug severity, a two-user access check, gate evidence rules and the audit log. Spool evidence is covered by API/Web tests. The V2.1 workspace (Direction D) and the requirement matrix are described in `../docs/v21/ACCEPTANCE.md`. To include actual DOCX rendering in the report test, set DRAUPNIR_TEST_STYLUS_EXECUTABLE to a local Stylus executable.

## Web（Nuxt 4）

The web app lives in `v2/web`. It is a client-side Nuxt app; `/api/**` is proxied by the Nuxt server to the API (`NUXT_API_BASE`, default `http://127.0.0.1:5080`), so the browser keeps the session cookie on the same origin.

~~~powershell
cd v2/web
npm install

# Start the API on the port the web app proxies to (see the API section above for env vars).
# In v2/api:
#   dotnet run --project src/Draupnir.Api --no-build --no-restore -- --urls http://127.0.0.1:5080

# Optional: point somewhere else when the API uses another port.
$env:NUXT_API_BASE = 'http://127.0.0.1:5080'
npm run dev          # http://localhost:3000
~~~

Sign in with `DRAUPNIR_ADMIN_EMAIL` / `DRAUPNIR_ADMIN_PASSWORD` configured for the API process.

Pages: login, project list, project dashboard, requirements, work items (table and drag/keyboard Kanban, Bug severity), sprints and milestones, agent runs, evidence and reviews (including Spool trace evidence), human gates (approval/rejection requires a confirmation dialog and is Admin-only), delivery reports (Markdown view and Stylus YAML download), Supervisor import, and `/users` (Admin only). The project overview has an Admin-only members panel. All UI text is Traditional Chinese. Loading, empty, and error states are implemented; `409`/`400` ProblemDetails are mapped to readable Chinese messages, and `401` redirects to the login page.

### Web checks

~~~powershell
cd v2/web
npm run typecheck    # nuxt typecheck (includes tests)
npm test             # Vitest unit/component tests
npm run build        # production build (.output)
~~~

### End-to-end tests (real API + PostgreSQL)

`npm run test:e2e` builds the web app, starts the Nuxt server with `NUXT_API_BASE`, and runs Playwright on desktop (1280) and mobile (360). It expects:

- PostgreSQL running on `127.0.0.1:55432` (see “Start PostgreSQL and migrate”).
- The separate test API running at `http://127.0.0.1:5088` with an administrator configured and a migrated database; use a dedicated disposable database such as `draupnir_e2e` or `draupnir_v21_fix2` (the guard refuses the preview). The E2E run creates projects, a Bug with severity, a second Member, and imports Supervisor data.
- The Playwright global setup is fail-closed: it calls anonymous `GET /api/health`, refuses unless the API reports `status=ok` and `disposable=true`, refuses the shared `draupnir` / `draupnir_v21` / `draupnir_design` databases, refuses a non-loopback API, and refuses without the explicit write opt-in, writable E2E_STATE_DIR, and a matching nonempty E2E_EXPECTED_INSTANCE / E2E_INSTANCE_TOKEN. Playwright never reuses an existing server. Run `npx vitest run tests/unit/e2eGuard.test.ts` for the guard proof.

~~~powershell
# Example API process for E2E (separate database, migrate first)
# Choose a unique non-secret marker and use exactly the same value in the Web terminal.
$env:E2E_INSTANCE_TOKEN = '<unique-test-instance-marker>'
$env:ConnectionStrings__Draupnir = 'Host=127.0.0.1;Port=55432;Database=draupnir_e2e;Username=postgres'
$env:DRAUPNIR_ADMIN_EMAIL = 'admin@draupnir.local'
$env:DRAUPNIR_ADMIN_PASSWORD = '<choose a password for this local instance>'
dotnet ef database update --project src/Draupnir.Api --no-build
dotnet run --project src/Draupnir.Api --no-build --no-restore -- --urls http://127.0.0.1:5088

# In a separate terminal, from v2/web:
$env:E2E_ALLOW_WRITE = '1'
$env:E2E_STATE_DIR = Join-Path $env:TEMP ('draupnir-e2e-' + [guid]::NewGuid().ToString('N'))
$env:E2E_EXPECTED_INSTANCE = '<same-unique-test-instance-marker>'
$env:E2E_ADMIN_PASSWORD = '<the API administrator password>'
$env:NUXT_API_BASE = 'http://127.0.0.1:5088'
$env:E2E_PORT = '4950'
npm run test:e2e
~~~

The flow covers login → create project → create requirement and two work items → add a dependency → attempt to move the dependent item to Done (blocked with a Chinese message) → import `tests/Draupnir.Api.Tests/Fixtures/queue.json` → approve a Human Gate through the confirmation dialog → generate and view a delivery report. It also runs axe (WCAG 2.1 A/AA) on the main pages and checks 360px pages for horizontal scrolling. Configure `E2E_SCREENSHOT_DIR` to a dedicated local artifact directory. Preserve preview data and only clean up disposable test resources after confirming their identity.

## V2.1 frozen delivery

See [release readiness](../docs/v21/RELEASE-READINESS.md), [acceptance](../docs/v21/ACCEPTANCE.md), and [test summary](../docs/v21/TEST-SUMMARY.md). Local product direction accepted, feature-frozen, full owner UAT not recorded. Draft PR #5 is not merged, released or deployed.
