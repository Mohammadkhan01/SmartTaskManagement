# Requirement Traceability Report

Final traceability record required by the engineering handbook (§3.1,
Appendix H, Appendix K "Final traceability report completed"). Every
requirement ID is listed individually; checklists from §2.6, §12.5, §16.2,
Chapter 20, Appendix J and Appendix K follow.

Status legend:

| Status | Meaning |
|---|---|
| Done | Implemented **and** verified by an automated test or a recorded measurement/check. |
| Partial | Implemented, but verification is incomplete or a part of the requirement is missing. |
| Not done | Not implemented or not verified at all. |

Paths are relative to the repository root. `src/` = backend, `frontend/src/app/`
= Angular (abbreviated `fe/`), `tests/` = .NET test projects
(`UT` = `SmartTask.UnitTests`, `IT` = `SmartTask.IntegrationTests`,
`AT` = `SmartTask.ArchitectureTests`).

## Summary

**56 / 57 requirements Done, 1 Partial (REQ-047), 0 Not done.**

Verification run used for this report (2026-11-17):

| Suite | Result |
|---|---|
| `dotnet build SmartTaskManagement.sln` | 0 errors |
| Unit tests (`UT`) | 262 passed |
| Architecture tests (`AT`) | 8 passed |
| Integration tests (`scripts/run-integration-tests.sh`, SQL Server `SmartTaskTestDb`) | 48 passed |
| Frontend (`npx ng test --watch=false`, Vitest) | 193 passed |
| Frontend build (`npx ng build`) | succeeded |
| Live API `GET https://localhost:7251/health` | 200 |
| Performance smoke (`scripts/perf-smoke.sh`) | all Appendix G API targets met, see `docs/performance.md` |

## REQ-001 … REQ-057

| ID | Requirement | Implementation | Verification | Status |
|---|---|---|---|---|
| REQ-001 | N-Layer architecture | `src/SmartTask.API`, `.BLL`, `.DAL`, `.Domain`, `.Common`; `docs/architecture.md` | `AT/LayerDependencyTests.cs` (project reference rules), `AT/BusinessServiceBoundaryTests.cs`, `AT/ControllerBoundaryTests.cs` | Done |
| REQ-002 | Registration | `BLL/Services/Auth/AuthService.cs` (`RegisterAsync`), `Validators/Auth/RegisterRequestValidator.cs`, `API/Controllers/AuthController.cs` | `UT/AuthServiceTests.cs` (`RegisterAsync_*`); `fe/features/auth/login.component.spec.ts` | Done |
| REQ-003 | Login | `AuthService.LoginAsync`, `DAL/Identity/UserAccountRepository.cs` (lockout) | `UT/AuthServiceTests.cs`; `IT/UserAccountRepositoryLockoutTests.cs` (5 failures lock account); `IT/AuthRateLimitHttpIntegrationTests.cs` | Done |
| REQ-004 | Logout | `AuthService.LogoutAsync` revokes refresh token; `AuthController` clears `smarttask.refresh` cookie, returns 204 | `IT/AuthSecurityHttpIntegrationTests.cs` (`Refresh_AfterLogout_RejectsRevokedToken`); `UT/AuthServiceTests.cs` | Done |
| REQ-005 | JWT | `API/Authentication/JwtTokenService.cs`, `AuthenticationDependencyInjection.cs` (issuer/audience/lifetime/signing key, 30 s skew, `OnTokenValidated` active-user check) | `UT/JwtTokenServiceTests.cs`, `UT/JwtConfigurationTests.cs`; `IT/AuthSecurityHttpIntegrationTests.cs` (expired / tampered → 401); `IT/InactiveUserJwtHttpIntegrationTests.cs` | Done |
| REQ-006 | Refresh tokens | `AuthService` (SHA-256 `HashToken`, rotation, family revocation on reuse), `RefreshTokenConfiguration.cs` (unique hash index), HttpOnly cookie + CSRF header | `IT/AuthSecurityHttpIntegrationTests.cs` (`Refresh_WithRotatedToken_Returns401AndRevokesTokenFamily`); `UT/AuthServiceTests.cs` | Done |
| REQ-007 | Three user roles | `Common/Constants/RoleNames.cs`, `DAL/Identity/IdentityRoleSeeder.cs`; project roles `Domain/Enums/ProjectMemberRole.cs` | `IT/AdminDashboardHttpIntegrationTests.cs` (Admin vs Team Member); `IT/TaskHttpIntegrationTests.cs` (Project Manager); `DatabaseConstraintTests` (`CK_ProjectMembers_ProjectRole`) | Done |
| REQ-008 | Role authorization | `[Authorize(Roles = RoleNames.Admin)]` on `AdminUsersController`, `AdminDashboardController`; `fe/core/guards/admin.guard.ts` | `IT/AdminDashboardHttpIntegrationTests.cs` (401/403/200 for dashboard **and** `/api/v1/admin/users` list, status, role); `UT/AdminUserServiceTests.cs`; `fe/core/guards/guards.spec.ts` | Done |
| REQ-009 | Resource authorization | Membership/role checks in `ProjectService`, `TaskService`, `TaskCommentService`, `TaskAttachmentService`; membership-scoped queries in repositories | `IT/TaskHttpIntegrationTests.cs` (`TaskEndpoints_EnforceProjectMembershipAndTaskPermissions`), `IT/ProjectListHttpIntegrationTests.cs` (membership scope), `IT/TaskAttachmentHttpIntegrationTests.cs`, `IT/TaskCommentActivityHttpTests.cs`; `UT/ProjectServiceTests.cs` (`DeleteAsync_ShouldRejectNonOwner` etc.) | Done |
| REQ-010 | Project CRUD | `BLL/Services/Projects/ProjectService.cs`, `API/Controllers/ProjectsController.cs`, soft delete | `UT/ProjectServiceTests.cs` (create/update/delete/soft delete/audit); `IT/ProjectConcurrencyHttpTests.cs`, `IT/ProjectActivationTaskAssignmentHttpTests.cs`, `IT/ProjectListHttpIntegrationTests.cs` | Done |
| REQ-011 | Project search | `DAL/Persistence/Repositories/ProjectRepository.cs` (`Where` translated to SQL) | `IT/ProjectListHttpIntegrationTests.cs`; `UT/ProjectRepositoryPagedTests.cs` | Done |
| REQ-012 | Project sorting | Whitelist in `ProjectListQueryRequestValidator.cs` + switch in `ProjectRepository` | `UT/ProjectListQueryRequestValidatorTests.cs`; `IT/ProjectListHttpIntegrationTests.cs` | Done |
| REQ-013 | Project pagination | `Skip/Take` in `ProjectRepository`; `Common/Utilities/Pagination.cs`, `Common/Responses/PagedResponse.cs` | `UT/PaginationTests.cs`, `UT/ProjectRepositoryPagedTests.cs`; `IT/ProjectListHttpIntegrationTests.cs` (incl. invalid page size → 400) | Done |
| REQ-014 | Task CRUD | `BLL/Services/Tasks/TaskService.cs`, `API/Controllers/TasksController.cs`, soft delete | `UT/TaskServiceTests.cs`; `IT/TaskHttpIntegrationTests.cs` | Done |
| REQ-015 | Task assignment | `TaskService` membership validation, `AssignTaskRequestValidator.cs` | `IT/TaskHttpIntegrationTests.cs` (`AssignTask_AsProjectManager_PersistsAssignmentAndNotification`), `IT/ProjectActivationTaskAssignmentHttpTests.cs`; `UT/TaskServiceTests.cs` | Done |
| REQ-016 | Task status | `Domain/Enums/TaskStatus.cs`, `CK_Tasks_Status`, `UpdateTaskStatusRequestValidator.cs` | `IT/DatabaseConstraintTests.cs` (CHECK rejects invalid values); `UT/TaskServiceTests.cs`; `IT/TaskHttpIntegrationTests.cs` (`PatchTaskStatus_WithStaleRowVersion_Returns409Conflict`) | Done |
| REQ-017 | Task priority | `Domain/Enums/TaskPriority.cs`, `CK_Tasks_Priority` | `IT/DatabaseConstraintTests.cs`; `UT/TaskListQueryRequestValidatorTests.cs` | Done |
| REQ-018 | Task due dates | `TaskItem.DueDateUtc`, due-date filters in `TaskRepository`, index in `TaskItemConfiguration.cs` | `UT/TaskListQueryRequestValidatorTests.cs`; `IT/DashboardHttpIntegrationTests.cs` (overdue/upcoming) | Done |
| REQ-019 | Task search | `TaskRepository` SQL-side filters | `IT/TaskHttpIntegrationTests.cs`; `UT/TaskServiceTests.cs`; perf: `docs/performance.md` | Done |
| REQ-020 | Task sorting | Whitelist in `TaskListQueryRequestValidator.cs` + `TaskRepository` | `UT/TaskListQueryRequestValidatorTests.cs` | Done |
| REQ-021 | Task pagination | `TaskRepository` `Skip/Take`, `TaskListResponse : PagedResponse<T>` | `UT/PaginationTests.cs`, `UT/TaskListQueryRequestValidatorTests.cs`; `IT/TaskHttpIntegrationTests.cs` | Done |
| REQ-022 | Dashboard totals | `DashboardService`, `DashboardRepository` (SQL aggregates), `DashboardProjectTotalsResponse`; admin totals `AdminDashboardController` | `UT/DashboardServiceTests.cs`; `IT/DashboardHttpIntegrationTests.cs`; `IT/AdminDashboardHttpIntegrationTests.cs`; `fe/features/dashboard/dashboard.component.spec.ts` (project totals, admin overview) | Done |
| REQ-023 | Dashboard statistics | Status/priority aggregates in `DashboardRepository` | `IT/DashboardHttpIntegrationTests.cs`; `UT/DashboardServiceTests.cs` | Done |
| REQ-024 | Completed vs pending | `DashboardTaskStatisticsResponse` | `IT/DashboardHttpIntegrationTests.cs`; `dashboard.component.spec.ts` | Done |
| REQ-025 | Upcoming due tasks | `DashboardRepository` upcoming query (excludes Completed/Cancelled) | `IT/DashboardHttpIntegrationTests.cs` (`UpcomingTasks` ordering/count) | Done |
| REQ-026 | AI enhancement | `BLL/Services/AI/AIService.cs`, `TaskDescriptionPrompt.cs`, `DAL/Ai/MockAiTextProvider.cs`, `OpenAiCompatibleTextProvider.cs`, `AiController`; `fe/core/ai/ai.service.ts` | `UT/AIServiceTests.cs`, `UT/AiTextProviderTests.cs`, `UT/DescriptionLengthValidatorTests.cs` (4000/4001); `IT/AiHttpIntegrationTests.cs`; `fe/core/ai/ai.service.spec.ts`, `task-form.component.spec.ts` | Done |
| REQ-027 | Backend | ASP.NET Core / EF Core on .NET 10 (`*.csproj`) | Solution build; all backend suites | Done |
| REQ-028 | Database | SQL Server 2022 (`docker/`), `SmartTaskDbContext`, migrations | `IT/*` run against SQL Server; `IT/DatabaseHealthCheckTests.cs` | Done |
| REQ-029 | Async/await | All repository/service/controller I/O is `async` with `CancellationToken` | `GlobalExceptionHandlerTests` (client cancellation); code review | Done |
| REQ-030 | Validation | FluentValidation validators in `BLL/Validators/**` | `UT/*ValidatorTests.cs`, `UT/DescriptionLengthValidatorTests.cs`; `IT/GlobalExceptionHandlerTests.cs` (400 with errors) | Done |
| REQ-031 | API documentation | `MapOpenApi` + Swagger UI at `/swagger` (Development); `docs/api/openapi.json` (regenerated, 32 paths) | Export regenerated from running API; README section | Done |
| REQ-032 | Error handling | `API/Exceptions/GlobalExceptionHandler.cs` (ProblemDetails) | `IT/GlobalExceptionHandlerTests.cs` (400/401/409/500, no internals leaked) | Done |
| REQ-033 | Consistent responses | ProblemDetails for errors, `PagedResponse<T>` for lists, DTOs for bodies | `IT/GlobalExceptionHandlerTests.cs`; `UT/PaginationTests.cs`; `fe/core/api/api-error.spec.ts` | Done |
| REQ-034 | Password security | ASP.NET Identity hashing + password policy (`RegisterRequestValidator`, Identity options) | `UT/AuthServiceTests.cs`; `IT/UserAccountRepositoryLockoutTests.cs` | Done |
| REQ-035 | HTTPS | `UseHttpsRedirection`, `UseHsts` (non-Development), HTTPS launch profile; Secure refresh cookie | Live check: API served on `https://localhost:7251`, `/health` 200. **No automated redirect test** (TestServer has no HTTPS port) | Done |
| REQ-036 | CORS | Named policy `Frontend` with explicit origins; startup rejects `*`/non-HTTP(S) origins (`Program.cs`) | `IT/CorsHttpIntegrationTests.cs` (trusted origin allowed with credentials, untrusted origin not allowed) | Done |
| REQ-037 | Rate limiting | Fixed-window `auth` policy (10/60 s per IP) and per-user `Ai` policy | `IT/AuthRateLimitHttpIntegrationTests.cs` (429); `IT/AiHttpIntegrationTests.cs` | Done |
| REQ-038 | Secure configuration | User secrets / env vars; `docker/.env` git-ignored; `JwtConfigurationTests` enforce key length; CI uses secrets/throwaway values | `UT/JwtConfigurationTests.cs`; source scan (no secrets in tracked config) | Done |
| REQ-039 | Logging | Serilog host + `Logging/RequestLoggingConfiguration.cs` | `IT/SerilogRequestLoggingTests.cs` | Done |
| REQ-040 | Health checks | `/health` with self + `DatabaseHealthCheck` | `IT/DatabaseHealthCheckTests.cs`; live `/health` 200 | Done |
| REQ-041 | Angular | Angular 21 (`frontend/package.json`), TypeScript strict | `npx ng build`, 193 Vitest tests | Done |
| REQ-042 | Standalone components | All components standalone; `app.config.ts` uses `provideRouter`/`provideHttpClient` | `fe/app.spec.ts`, component specs | Done |
| REQ-043 | Authentication UI | `fe/features/auth/login.component.ts` (login + register), `core/auth/*` | `login.component.spec.ts`, `auth.service.spec.ts`, `session-store.spec.ts`; screenshot `01-register-password-rules.png` | Done |
| REQ-044 | Dashboard UI | `fe/features/dashboard/dashboard.component.ts` | `dashboard.component.spec.ts`; screenshots `02-dashboard.png`, `08-…` | Done |
| REQ-045 | Project UI | `fe/features/projects/*` | `projects.component.spec.ts`, `project-details.component.spec.ts`, `project.service.spec.ts`; screenshot `04-…` | Done |
| REQ-046 | Task UI | `fe/features/tasks/*` | `task-list/-form/-details.component.spec.ts`, `task.service.spec.ts`; screenshots `05–07` | Done |
| REQ-047 | Responsive UI | `@media` breakpoints in all 9 feature/layout components | **No automated or recorded tablet/mobile verification**; all screenshots are desktop-width | **Partial** |
| REQ-048 | Route guards | `fe/core/guards/auth.guard.ts`, `admin.guard.ts`, `app.routes.ts` | `guards.spec.ts`, `app.routes.spec.ts` | Done |
| REQ-049 | HTTP interceptors | `fe/core/interceptors/auth.interceptor.ts` (bearer, CSRF, refresh-on-401, error mapping) | `auth.interceptor.spec.ts`, `api-error.spec.ts` | Done |
| REQ-050 | Reactive forms | `FormBuilder`/`FormGroup` in login, project and task forms | `login.component.spec.ts`, `task-form.component.spec.ts`, `projects.component.spec.ts` | Done |
| REQ-051 | Lazy loading | `loadComponent` for every feature route in `app.routes.ts` | `app.routes.spec.ts`; `ng build` emits lazy chunks | Done |
| REQ-052 | Unit tests | `tests/SmartTask.UnitTests`, `tests/SmartTask.ArchitectureTests`, `frontend/**/*.spec.ts` | 262 + 8 + 193 passing | Done |
| REQ-053 | Integration tests | `tests/SmartTask.IntegrationTests` (WebApplicationFactory + real SQL Server), `scripts/run-integration-tests.sh`, CI service container | 48 passing | Done |
| REQ-054 | README | `README.md` | Claims cross-checked against code in this pass | Done |
| REQ-055 | PROMPTS.md | `PROMPTS.md` | Matches `TaskDescriptionPrompt.cs` | Done |
| REQ-056 | API collection | `docs/api/SmartTask.postman_collection.json` + environment, `docs/api/openapi.json` | Files present; OpenAPI regenerated from live API | Done |
| REQ-057 | Database setup | EF Core migrations in `src/SmartTask.DAL/Persistence/Migrations`, `docs/database/README.md` (idempotent script command) | Migrations applied to `SmartTaskDb` and `SmartTaskTestDb`; latest migration verified up → down → up | Done |

## §2.6 Pre-Development Approval Gate / Appendix K "Before Coding"

These are process sign-offs. The handbook boxes are unchecked and no record of a
formal approval exists in the repository, so they cannot be claimed as done.

| Item | Status | Evidence |
|---|---|---|
| Original assignment reviewed line by line | Not done (no record) | Original assignment text is not in the repo |
| All requirements listed / requirement matrix completed | Done | Handbook §2.3; this document |
| Architecture approved | Not done (no sign-off) | Design documented in `docs/architecture.md` |
| Database design / ERD / schema approved | Not done (no sign-off) | ERD in `docs/database/README.md` |
| Roles and permissions defined | Done | Handbook Appendix D; `RoleNames.cs`, `ProjectMemberRole` |
| API plan / contract | Done | Appendix E; `docs/api/openapi.json` |
| Frontend page list | Done | `app.routes.ts` |
| Testing strategy approved | Not done (no sign-off) | Strategy exists in handbook Ch. 15 |
| Exclusions documented | Done | Appendix I; see Deviations below |
| No critical ambiguity remains | Done | No open ambiguity recorded |

## §12.5 / §20.4 Security Acceptance

| Item | Status | Evidence |
|---|---|---|
| Passwords securely hashed | Done | ASP.NET Identity hasher; `AuthServiceTests` |
| JWT validation correct | Done | `AuthSecurityHttpIntegrationTests` (expired, tampered), `JwtConfigurationTests`, `InactiveUserJwtHttpIntegrationTests` |
| Refresh tokens securely stored | Done | SHA-256 hash only, unique index, HttpOnly/Secure cookie, reuse detection test |
| Role restrictions work | Done | `AdminDashboardHttpIntegrationTests` (dashboard + admin users 401/403/200) |
| Resource restrictions work / unauthorized project & task access blocked | Done | `TaskHttpIntegrationTests`, `ProjectListHttpIntegrationTests`, attachment/comment HTTP tests |
| Task assignment rules enforced | Done | `TaskHttpIntegrationTests`, `ProjectActivationTaskAssignmentHttpTests` |
| CORS restricted | Done | `CorsHttpIntegrationTests`; startup guard against `*` |
| Rate limiting configured | Done | `AuthRateLimitHttpIntegrationTests` |
| HTTPS configured | Done (manual) | HTTPS redirection + HSTS in `Program.cs`; live HTTPS endpoint. No automated test |
| Secrets not committed | Done | `docker/.env` ignored; dev secrets in user-secrets; CI uses `secrets.*` or throwaway values |
| Production errors do not expose internals | Done | `GlobalExceptionHandlerTests` (`…Returns500WithoutLeakingDetails`, `…409WithoutLeakingDetails`) |
| Account lockout | Done | `UserAccountRepositoryLockoutTests` |
| Input validation | Done | FluentValidation everywhere; validator tests |
| IDOR protection / authorization tests | Done | see resource restrictions |
| CSRF on cookie-based refresh | Done | `/api/v1/auth/csrf` + `X-CSRF-TOKEN`; `auth.interceptor.spec.ts` (CSRF header handling), refresh flow in `AuthSecurityHttpIntegrationTests` |

## §16.2 Audit Event Coverage

Persisted in `AuditLogs` (bigint identity, `EntityName`, `TimestampUtc`,
`IpAddress` via `ICurrentRequestContext`):

| Entity | Actions | IP captured | Test evidence |
|---|---|---|---|
| Project | `ProjectCreated`, `ProjectUpdated`, `ProjectStatusChanged`, `ProjectDeleted`, `ProjectMemberAdded`, `ProjectMemberRemoved` | Yes | `UT/ProjectServiceTests.cs` (incl. IP assertion) |
| Task | `TaskCreated`, `TaskUpdated`, `TaskAssigned`, `TaskStatusChanged`, `TaskDeleted` | Yes | `UT/TaskServiceTests.cs` (incl. IP assertion), `IT/TaskCommentActivityHttpTests.cs` (activity feed) |
| TaskComment / TaskAttachment | `CommentCreated`, `CommentUpdated`, `CommentDeleted`, `AttachmentUploaded`, `AttachmentDeleted` | Yes | `UT/TaskCommentServiceTests.cs`, `UT/TaskAttachmentServiceTests.cs` |
| User | `UserStatusChanged`, `UserRoleChanged` | Yes | `UT/AdminUserServiceTests.cs` (IP), `IT/AdminDashboardHttpIntegrationTests.cs` (persisted row) |

Not audited: login/logout/refresh events (only operational Serilog request logs).
The handbook does not list these as mandatory audit events.

## Chapter 20 Acceptance

### 20.1 Architecture

| Item | Status | Evidence |
|---|---|---|
| N-Layer structure implemented | Done | 5 projects; `AT/LayerDependencyTests.cs` |
| Responsibilities clearly separated | Done | `AT/BusinessServiceBoundaryTests.cs`, `AT/ControllerBoundaryTests.cs` |
| Dependency rules respected / no circular dependencies | Done | Architecture tests (project references form a DAG) |
| Controllers remain thin | Done | Controllers depend only on `BLL.Interfaces.Services` (architecture test) |
| Business rules reside in services | Done | `BLL/Services/**` |
| Persistence logic resides in DAL | Done | `DAL/Persistence/Repositories/**` |
| No unnecessary abstractions | Done | No MediatR/CQRS; one interface per service/repository |

### 20.2 Functional

All items (registration, login, logout, JWT, refresh rotation/revocation, three
roles, project CRUD/search/sort/paging, task CRUD/assignment/status/priority/
search/sort/paging, dashboard totals/statistics, upcoming tasks, AI
enhancement): **Done**; see REQ-002…REQ-026 above.

### 20.3 Database

| Item | Status | Evidence |
|---|---|---|
| All required tables created | Done | `docs/database/README.md` table reference; migrations |
| Primary keys configured | Done | Entity configurations; `AuditLogs.Id` bigint identity (`DatabaseConstraintTests`) |
| Foreign keys configured / relationships verified | Done | Configurations; FK violations surface in integration tests |
| Unique constraints verified | Done | `ProjectMembers` composite PK (duplicate rejected), unique refresh-token hash index |
| Enum constraints verified | Done | `IT/DatabaseConstraintTests.cs` (Tasks Status/Priority, Projects Status, ProjectMembers ProjectRole) |
| Indexes reviewed | Partial | Indexes justified in `docs/database/README.md`; **execution plans not captured** |
| Soft deletion verified | Done | `ProjectServiceTests.DeleteAsync_ShouldSoftDeleteProjectForOwner`, global query filters |
| RowVersion concurrency verified | Done | `ProjectConcurrencyHttpTests`, `ProjectRowVersionConcurrencyTests`, task 409 tests |
| Refresh tokens stored as hashes | Done | `AuthService.HashToken`; unit tests |
| Audit history preserved | Done | `AlignAuditLogSchema` migration copies existing rows; FK `Restrict`; `DatabaseConstraintTests` |
| Migrations reproducible | Done | Applied to dev/test DBs; up/down/up verified; CI runs migrations on a fresh container |

### 20.4 Security

See the security table above. All Done; HTTPS verified manually only.

### 20.5 Quality

| Item | Status | Evidence |
|---|---|---|
| Unit tests pass | Done | 262 + 8 architecture |
| Integration tests pass | Done | 48 |
| Frontend tests pass | Done | 193 |
| Failure paths tested | Done | 400/401/403/404/409/429/5xx tests across suites |
| No known critical security defect | Done | None known |
| No known critical functional defect | Done | None known |
| Important queries measured | Partial | API latency measured (`docs/performance.md`); dataset small, query plans not reviewed |
| Logging structured and safe | Done | `SerilogRequestLoggingTests` |
| Health checks work | Done | `DatabaseHealthCheckTests`; live 200 |

### 20.6 Documentation

| Item | Status | Evidence |
|---|---|---|
| README.md | Done | `README.md` |
| PROMPTS.md | Done | `PROMPTS.md` |
| API collection or Swagger export | Done | `docs/api/` |
| Database migrations or SQL script | Done | Migrations + script command in `docs/database/README.md` |
| ERD | Done | Mermaid `erDiagram` in `docs/database/README.md` |
| Architecture diagram | Done | Mermaid diagrams in `docs/architecture.md` |
| Screenshots | Done | `docs/screenshots/` (8, desktop only) |
| Setup instructions verified | Done | Followed during this pass (user-secrets, docker SQL, migrations, API, Angular) |
| Documentation matches implementation | Done | Cross-checked in this pass; Mermaid not rendered in CI (see gaps) |

## Appendix J — Database Implementation Checklist

| Item | Status | Evidence |
|---|---|---|
| Tables identified; ERD reviewed | Done | `docs/database/README.md` |
| Primary/foreign keys defined | Done | `DAL/Persistence/Configurations/*` |
| Nullable fields reviewed; data types selected | Done | Configurations (max lengths from `FieldLimits.cs`, `IpAddress` varchar(45)) |
| Relationships / delete behaviours defined and configured | Done | Every configured FK uses `DeleteBehavior.Restrict`; deletion is soft (no cascades) |
| Enum values documented | Done | `docs/database/README.md` |
| Indexes justified and configured | Done | Configurations + DB README |
| Unique constraints defined | Done | ProjectMembers PK, RefreshToken hash, Identity indexes |
| Check constraints reviewed | Done | 4 CHECK constraints, tested in `DatabaseConstraintTests` |
| ApplicationDbContext created | Done | Named `SmartTaskDbContext` (naming deviation only) |
| Identity configured | Done | `ApplicationUser`, Identity stores |
| Entity / IEntityTypeConfiguration classes created | Done | `Domain/Entities`, `DAL/Persistence/Configurations` |
| Concurrency tokens configured | Done | `IsRowVersion()` on Project, TaskItem, TaskComment, TaskAttachment |
| Initial migration created; generated SQL reviewed | Done | Migrations folder; `AlignAuditLogSchema` hand-written after reviewing the scaffold |
| Development database created; seed roles verified | Done | `SmartTaskDb`; `IdentityRoleSeeder` (roles used by integration tests) |
| Duplicate project membership rejected | Done | Composite PK; `ProjectServiceTests` |
| Invalid enum values rejected | Done | `DatabaseConstraintTests` |
| Invalid foreign keys rejected | Done | SQL FKs |
| Task assignment membership checked | Done | `TaskHttpIntegrationTests` |
| Soft deletion works; historical task records preserved | Done | Global query filters; `ProjectServiceTests` soft-delete tests; Restrict FKs keep task history |
| Refresh-token hashes unique | Done | Unique index |
| Concurrent updates detected | Done | 409 tests |
| Audit history preserved | Done | See 20.3 |
| Project/task listing uses SQL pagination; search in SQL; sorting whitelisted | Done | Repositories + validator tests |
| Dashboard uses SQL aggregates | Done | `DashboardRepository` |
| Upcoming tasks use indexed date filtering | Done | `DueDateUtc` index |
| No unnecessary N+1 queries | Done | Projections in repositories |
| Important query plans reviewed | **Not done** | No execution plans captured |

## Appendix K — Final Quality Checklist

| Section / item | Status | Evidence |
|---|---|---|
| Before Coding | Partial | See §2.6 table (formal approvals not recorded) |
| Implement only approved scope | Partial | Extras beyond REQ list: notifications, comments, attachments, admin user management, admin dashboard (see Deviations) |
| Follow layer responsibilities | Done | Architecture tests |
| Use DTOs and validation | Done | `BLL/DTOs`, `BLL/Validators` |
| Enforce authorization on the backend | Done | Security table |
| Write tests alongside features | Done | Test suites per feature |
| Handle errors consistently | Done | `GlobalExceptionHandler` |
| Log safely | Done | Query-string privacy test |
| Update traceability status | Done | This document |
| Record approved scope changes | Partial | Extras documented here and in handbook §16.2, no approval record |
| All mandatory requirements implemented | Done | 57/57 implemented |
| All mandatory requirements tested | Partial | REQ-047 not verified; HTTPS manual only |
| Security checks passed | Done | Security table |
| Frontend requirements verified | Partial | REQ-047 |
| AI functionality verified | Done | AI unit/integration/frontend tests |
| Database setup reproducible | Done | Migrations, CI fresh DB |
| API documentation complete | Done | OpenAPI + Postman |
| README.md / PROMPTS.md complete | Done | |
| API collection/export included | Done | |
| Architecture diagram / ERD / screenshots included | Done | |
| Documentation matches implementation | Done | |
| No unresolved critical defects | Done | |
| Final traceability report completed | Done | This document |

## Deviations and Remaining Gaps

1. **REQ-047 responsive UI**: breakpoints exist in the CSS, but no tablet/mobile
   verification is recorded and all screenshots are desktop-width.
2. **HTTPS (REQ-035)** is verified only manually against the live API; there is no
   automated redirect/HSTS test.
3. **Query plans** (Appendix J, §20.3 "Indexes reviewed", §20.5 "Important queries
   measured") have not been captured. Performance figures in `docs/performance.md`
   come from a small dataset (the demo Team Member sees 1 project, 0 tasks).
4. **Angular initial load (Appendix G, < 3 s)** has not been measured; only the
   bundle size is reported (285.23 kB raw / 80.34 kB transfer).
5. **CI workflow** (`.github/workflows/ci.yml`) has run on GitHub and passed
   ([CI #2](https://github.com/Mohammadkhan01/SmartTaskManagement/actions)); the README
   badge points at `Mohammadkhan01/SmartTaskManagement`.
6. **Mermaid diagrams** (ERD, architecture) are hand-written and have not been rendered or
   validated by a tool.
7. **Process approvals** (§2.6, Appendix K "Before Coding") have no recorded sign-off.
8. **Scope extras** beyond REQ-001…057: in-app notifications, task comments, task
   attachments, task activity feed, admin user management and admin dashboard. These are
   referenced in the handbook (§16.2) but were not part of the original requirement list.
9. **No dedicated HTTP test for project DELETE**; it is covered by `ProjectServiceTests`
   unit tests (soft delete, non-owner rejection, audit).
10. **`Projects.IsDeleted` has no SQL default constraint**; EF always supplies the value.
11. **Authentication events** (login, logout, refresh) are not written to `AuditLogs`, only
    to Serilog request logs.
12. **Dedicated database-operation logging** is not implemented (already acknowledged in handbook §16.4).
