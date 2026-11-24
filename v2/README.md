# AstraDraupnir V2

The V2 API runs locally with .NET SDK 10, EF Core 10, and PostgreSQL 18. The original Angular app remains at the repository root. The V2 web app is a separate deliverable.

## Start PostgreSQL and migrate

Use the existing portable PostgreSQL instance on 127.0.0.1:55432. It uses local trust authentication for development. Start it from the Astra delivery workspace if it is stopped.

~~~powershell
pwsh -NoProfile -File E:/Astra/.astra-runs/AUTONOMOUS-DELIVERY/tools/pg-local.ps1 start
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

### Spool results

`POST /api/projects/{id}/spool-results` accepts a `spool-result/v1` JSON body (`schema`, `command`, `status`, `exitCode`, `findings`, `counts`, optional `affectedTraces`). It stores a `Trace` evidence row named after the command, with the original JSON in `data`; `status=pass` maps to `Pass`, `status=fail` to `Fail`. A SHA-256 of the payload makes re-import idempotent. `GET /api/projects/{id}/spool-results` and the Evidence page show the command, status, counts and affected traces. Invalid schema/status/arrays are `400`.

POST /api/import/supervisor accepts either Supervisor queue.json or delivery state/tasks.json as a JSON body. Pass ?projectId=... to associate the payload with an existing project. Otherwise import matches the task repository to a project or creates a project for it. Reimport updates identified records and preserves human decisions. A reviewer assignment by itself does not produce a verdict.

GET /api/projects/{id}/work-items accepts status, type, sprintId, q, page, and pageSize. It returns items, total, page, and pageSize. DELETE /api/projects/{id} archives the project. The dashboard activeSprintCompletionRate is a percentage from 0 to 100.

POST /api/projects/{id}/reports creates a Markdown report and Stylus DocumentSpec. Fetch them from /api/reports/{id}/markdown and /api/reports/{id}/stylus.yaml. The spec uses JSON syntax, which Stylus accepts as YAML. If the native Stylus executable is on PATH or configured by DRAUPNIR_STYLUS_EXECUTABLE, the API also attempts to render DOCX. Set DRAUPNIR_REPORT_DIR to control where the generated files are written. The default is reports/ under the API process working directory. Markdown and YAML remain available when Stylus is unavailable.

## Tests

~~~powershell
cd v2/api
dotnet build --no-restore
dotnet test --no-build --no-restore
~~~

Integration tests use WebApplicationFactory and PostgreSQL. The test connection defaults to the local postgres maintenance database at port 55432. DRAUPNIR_TEST_POSTGRES overrides it. The integration test fixture creates, migrates, and removes its own draupnir_test_<guid> database. Current tests: API 32/32, Web 40/40, Playwright 26/26 (desktop 1280 and 390px; delivery flow, V2.1 acceptance and keyboard/a11y); the E2E includes Bug severity, a two-user access check, gate evidence rules and the audit log. Spool evidence is covered by API/Web tests. The V2.1 workspace (Direction D) and the requirement matrix are described in `../docs/v21/ACCEPTANCE.md`. To include actual DOCX rendering in the report test, set DRAUPNIR_TEST_STYLUS_EXECUTABLE to a local Stylus executable.

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
- The API running at `http://127.0.0.1:5080` with an administrator configured and a migrated database (use a dedicated database such as `draupnir_e2e`; the E2E run creates projects, a Bug with severity, a second Member, and imports Supervisor data).
- Optional env: `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD` (defaults `admin@draupnir.local` / `Draupnir!2026`), `NUXT_API_BASE`, `E2E_PORT`.

~~~powershell
# Example API process for E2E (separate database)
$env:ConnectionStrings__Draupnir = 'Host=127.0.0.1;Port=55432;Database=draupnir_e2e;Username=postgres'
$env:DRAUPNIR_ADMIN_EMAIL = 'admin@draupnir.local'
$env:DRAUPNIR_ADMIN_PASSWORD = 'Draupnir!2026'
dotnet run --project src/Draupnir.Api --no-build --no-restore -- --urls http://127.0.0.1:5080

cd ../web
npm run test:e2e
~~~

The flow covers login → create project → create requirement and two work items → add a dependency → attempt to move the dependent item to Done (blocked with a Chinese message) → import `tests/Draupnir.Api.Tests/Fixtures/queue.json` → approve a Human Gate through the confirmation dialog → generate and view a delivery report. It also runs axe (WCAG 2.1 A/AA) on the main pages and checks 360px pages for horizontal scrolling. Screenshots are written to `E:\Astra\.astra-runs\AUTONOMOUS-DELIVERY\acceptance\screens\draupnir` (override with `E2E_SCREENSHOT_DIR`).
