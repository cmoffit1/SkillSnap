# SkillSnap project guidance

## Project
- Purpose: You’re building SkillSnap, your own full-stack portfolio and project tracker.

## Technology
- Language/framework: Blazor WebAssembly client, ASP.NET Core Web API with controllers, C#, .NET 10 (SDK 10.0.401), Entity Framework Core 10 (code first, SQL Server).
- Key dependencies: Microsoft.EntityFrameworkCore.SqlServer and .Design, Azure.Extensions.AspNetCore.Configuration.Secrets, Azure.Identity, xUnit.
- Layout: `SkillSnap.Api` (REST API), `SkillSnap.Client` (Blazor WebAssembly), `tests\SkillSnap.Api.Tests` (xUnit). Never place a project at the repo root, because its globbing would include the nested projects.
- How to build and test: `dotnet build .\SkillSnap.sln`, `dotnet test .\SkillSnap.sln`. Run the API with `dotnet run --launch-profile https --project .\ SkillSnap.Api\SkillSnap.Api.csproj` (https://localhost:7250); the web app runs on https://localhost:7292.
- Database: local SQL Server 2025 Express (`.\SQLEXPRESS`), database `SkillSnap`. Migrations live in ` SkillSnap.Api\Data\Migrations`; add them with the local `dotnet ef` tool, for example `dotnet ef migrations add <Name> --project .\ SkillSnap.Api\SkillSnap.Api.csproj --output-dir Data\Migrations`.

## Conventions
- Follow the existing patterns in the codebase.
- Keep changes focused and avoid unrelated edits.
- Add or update tests for behavior changes.
- Use the newer C# language features like primary constructors, collection expressions and record types. Use plain property-based classes for EF entities, and records for DTOs.
- The API uses controllers (`[ApiController]` classes in ` SkillSnap.Api\Controllers`), not minimal APIs. Keep `Program.cs` limited to configuration.
- Controllers return DTOs, never EF entities where the shape differs; project in the query with `Select`.
- Read-only queries use `AsNoTracking()`. List endpoints are paged (default 25, maximum 100).
- Keep the Blazor/API boundary explicit: UI components call typed, scoped client services in `SkillSnap.Client\Services`; services inherit `ApiClientBase` rather than making ad hoc `HttpClient` requests. `ApiClientBase` supplies the API-version and bearer-token headers and converts transport and non-success responses to `ApiException`.
- The API and client currently define their DTO/request records separately (`SkillSnap.Api.Models` and `SkillSnap.Client.Services`). Keep corresponding wire shapes in sync; do not return EF entities or bind EF entities directly from client requests. Add request validation on API request records and return response DTOs.
- For controller list actions, normalize paging through `PagedResult<T>.Normalize`, apply deterministic ordering before `Skip`/`Take`, project to a DTO/row shape in the query, and return the standard `PagedResult<T>` envelope. Use `CancellationToken` for async request/database work. For successful creates, return `CreatedAtRoute` with the resource location and response DTO.
- Database access goes through `SkillSnapContext`, which is registered with pooling and `EnableRetryOnFailure`. Code that opens its own transaction must use `Database.CreateExecutionStrategy()`.
- Schema changes require a new EF migration; never edit an applied migration.

## Important rules
- Never commit secrets or connection strings. Locally they live in user secrets (key `ConnectionStrings:SkillSnap`); in Azure they come from Key Vault (secret name `ConnectionStrings--SkillSnap`, vault address in `KeyVault:Uri`).
- The development seeder (`DevelopmentDataSeeder`) runs only in the Development environment. Apply migrations from the CI/CD pipeline in Azure.
- Auth: ASP.NET Core Identity (`ApplicationUser`, in `SkillSnapContext`) with JWT bearer tokens issued by `AuthController` (`/auth/register`, `/auth/login`, admin-only `/auth/users` and role endpoints). Roles: Admin, Manager, Staff (constants in `Auth\Roles.cs`). Reads need any signed-in user; POST orders = any role; POST inventory = Admin/Manager; DELETE = Admin. Self-registration creates Staff only. Secrets: `Jwt:Key` (32+ chars), `Seed:AdminEmail`, `Seed:AdminPassword` in user secrets locally; `Jwt--Key` in Key Vault. New endpoints must carry `[Authorize]`.
- I want project to combine the two sub projects.  I will have a blazor frontend and a separate backend service, but they should work seamlessly together.
- they must use SSL for secure communication between the frontend and backend.
- The backend service should expose a RESTful API for the frontend to interact with.
- Authentication and authorization should be implemented to secure sensitive operations.
- The frontend should provide a responsive and user-friendly interface
- Error handling and logging should be implemented to facilitate debugging and monitoring.
- The system should be designed to scale efficiently as the number of users as the list of projects grows.
- The system should ensure data consistency and integrity across all operations.
- The system should provide comprehensive documentation for both the frontend and backend components.
- The system should include automated tests to ensure the correctness of critical functionality.
- The system should follow best practices for security, performance, and maintainability.
- The system should be designed with modularity and extensibility in mind to accommodate future requirements.
- The system should prepared to be checked into github with actions to deploy to azure
- The system should include CI/CD pipelines to automate testing and deployment processes.
- The system will use SQL server both locally and in the cloud for data storage.
- CI/CD pipelines should use free tier services whenever possible to minimize costs.
- The system should include monitoring and alerting to detect and respond to issues promptly.
- The system will use azure key vault for managing secrets and sensitive configuration data.

- Caching: in-memory only for now. `ListCache` (`SkillSnap.Api\Data\ListCache.cs`) caches paged project/skill lists for 1 minute, keyed by set, owner scope, page and size; every create/update/seed calls `cache.Invalidate(CacheSets.X)`. The skill level list is held in memory for 24 hours by `SkillLevelCache` and loaded at startup by the `SkillLevelWarmup` hosted service (falls back to loading on first use if the database isn't ready); call `SkillLevelCache.Invalidate()` when levels change. Single-item GETs are not cached.


- Controllers carry `[ApiVersion("1.0")]`; versions are read from the `api-version` header or query string only (no URL segments).
- Production hardening in place: ProblemDetails error handling, rate limiting (`RateLimitOptions`, `[EnableRateLimiting]` on auth endpoints), CORS from `Cors:AllowedOrigins`, security headers, correlation IDs, response compression, global `ETagFilter`, health checks (`/health/live`, `/health/ready`), `RowVersion` concurrency (409), `Idempotency-Key` on `POST /orders`, `AuditInterceptor` audit rows (use `AuditInterceptor.NewEntry` after `ExecuteDelete`, which bypasses it), refresh tokens (`/auth/refresh`, `/auth/logout`, rotation with reuse detection).
- `RowVersion` is only a concurrency token on SQL Server (`Database.IsSqlServer()`); SQLite and InMemory tests use a plain column.
- The test `ApiFactory` raises the auth rate limit; use `LowRateLimitFactory` to test 429 responses.
- Migrations are applied by the deploy workflow (EF bundle) or `dotnet ef database update` locally; the app does not migrate at startup.
- Data visibility: projects and skills are per user. List and single-item GETs return only the caller's own records (a record that isn't theirs is 404); `PortfolioOwnership.GetVisibleOwnerIdAsync` decides this. Admins see their own by default and everyone's with `?all=true` (ignored for non-admins). The client keeps that choice in `UserState.ViewAll` and shows a switch on the list pages to admins only.
- Deletes: `DELETE /api/projects/{id}` and `DELETE /api/skills/{id}` are admin-only (204, or 404 if missing), use a tracked remove so `AuditInterceptor` records them, and invalidate the list cache. The detail pages show an admin-only Delete button with a browser `confirm` prompt, then return to the list.
- Azure CI/CD: `.github\workflows` (ci, infra, deploy, codeql), `.github\dependabot.yml`, `infra\main.bicep` (App Service F1 hosting API + client, free Azure SQL, Key Vault, App Insights). Setup in `docs\azure-deployment.md`. Key Vault secrets: `ConnectionStrings--DefaultConnection`, `Jwt--Key`, `Seed--AdminEmail`, `Seed--AdminPassword`.
- Edits: `PUT /api/projects/{id}` and `PUT /api/skills/{id}` are admin-only. Project `ImageUrl` must be an absolute http/https URL (`[HttpUrl]` in the API, mirrored in `SkillSnap.Client\Components\HttpUrlAttribute.cs`).
- Skill level is a lookup: table `SkillLevels` (Id, Name, SortOrder; seeded Beginner, Intermediate, Advanced, Expert), `Skill.SkillLevelId` FK (restrict delete). Requests send `SkillLevelId`; `SkillDto` returns `SkillLevelId` and the level `Level` name. Unknown IDs return 400. `GET /api/skilllevels` feeds the client dropdown. There is no admin UI for managing levels yet; add them through a migration.
- Pages: `/projects` and `/skills` are list-only with an Add button; each row links to a read-only detail page (with Back and an admin-only Edit button). Edit/add forms live in `ProjectEdit`/`SkillEdit`, use `UnsavedChangesGuard` (dirty warning, blocks navigation while saving) and a Cancel/Close button. A successful edit returns to the detail page; a successful add clears the form so another can be entered.
- `UserState` (`SkillSnap.Client\Auth`) is the client's source of truth for the signed-in user (email, roles, `IsAdmin`); read it instead of parsing claims in components.
- The auth rate limit is configurable with `RateLimit:AuthPermitLimit` (default 10 per minute per IP).
- Blazor: the frontend is the WebAssembly app in `SkillSnap.Client`. Configure its API base address through `ApiBaseAddress`; outside Development it must use HTTPS and the same origin as the client. Keep authentication/token handling in the client auth provider and shared API client base, and surface API failures through the existing `ApiException` handling pattern.
- CI/CD: `.github\workflows` (ci, deploy, codeql), `.github\dependabot.yml`, infrastructure in `infra\main.bicep`, k6 load test in `loadtests\`.