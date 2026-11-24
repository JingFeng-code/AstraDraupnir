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

POST /api/import/supervisor accepts either Supervisor queue.json or delivery state/tasks.json as a JSON body. Pass ?projectId=... to associate the payload with an existing project. Otherwise import matches the task repository to a project or creates a project for it. Reimport updates identified records and preserves human decisions. A reviewer assignment by itself does not produce a verdict.

GET /api/projects/{id}/work-items accepts status, type, sprintId, q, page, and pageSize. It returns items, total, page, and pageSize. DELETE /api/projects/{id} archives the project. The dashboard activeSprintCompletionRate is a percentage from 0 to 100.

POST /api/projects/{id}/reports creates a Markdown report and Stylus DocumentSpec. Fetch them from /api/reports/{id}/markdown and /api/reports/{id}/stylus.yaml. The spec uses JSON syntax, which Stylus accepts as YAML. If the native Stylus executable is on PATH or configured by DRAUPNIR_STYLUS_EXECUTABLE, the API also attempts to render DOCX. Set DRAUPNIR_REPORT_DIR to control where the generated files are written. The default is reports/ under the API process working directory. Markdown and YAML remain available when Stylus is unavailable.

## Tests

~~~powershell
cd v2/api
dotnet build --no-restore
dotnet test --no-build --no-restore
~~~

Integration tests use WebApplicationFactory and PostgreSQL. The test connection defaults to the local postgres maintenance database at port 55432. DRAUPNIR_TEST_POSTGRES overrides it. The integration test fixture creates, migrates, and removes its own draupnir_test_<guid> database. To include actual DOCX rendering in the report test, set DRAUPNIR_TEST_STYLUS_EXECUTABLE to a local Stylus executable.
