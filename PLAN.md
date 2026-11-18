# Todo AI Factory — Phase 1 architecture plan

Status: approved by the human owner in the Band room on 2026-11-18; Phase 2 implementation is authorized.

## Scope and decisions

Build a small, single-user Todo Management application. The first release supports listing, filtering by completion, viewing, creating, editing, completing/reopening, and deleting todos. No accounts, sharing, notifications, recurring tasks, labels, or deployment pipeline are in scope. The API and Angular app are separate processes; the API owns validation and persistence. All stored timestamps are UTC. Deletion is permanent and initiated only for an individual todo by an explicit user action.

## Solution and dependency direction

| Project | Responsibility | Project references |
| --- | --- | --- |
| `Todo.Domain` | `TodoItem` entity, `TodoPriority` enum, entity invariants | None |
| `Todo.Application` | DTOs, use cases, validation, `ITodoItemStore` persistence port | Domain |
| `Todo.Infrastructure` | EF Core 9 SQL Server context, configuration, migrations, store implementation | Application, Domain |
| `Todo.API` | HTTP controllers, error handling, dependency injection, environment configuration | Application, Infrastructure |
| `Todo.Tests` | xUnit unit and API integration tests | Projects needed by each test |
| `todo-web` | Angular TypeScript UI and frontend tests | HTTP contract only; no .NET project reference |

Use one feature-focused application service and one todo-specific persistence interface. Controllers translate HTTP to DTOs and delegate. Domain has no EF Core or ASP.NET Core dependency. Infrastructure implements the application port using asynchronous EF Core calls. Avoid generic repositories, CQRS frameworks, mediator packages, and separate microservices.

## Proposed repository structure

```text
PLAN.md
AGENTS.md
Todo.sln
src/
  Todo.Domain/Entities/TodoItem.cs
  Todo.Domain/Enums/TodoPriority.cs
  Todo.Application/DTOs/Todos/
  Todo.Application/Interfaces/ITodoItemStore.cs
  Todo.Application/Services/TodoService.cs
  Todo.Infrastructure/Data/TodoDbContext.cs
  Todo.Infrastructure/Data/Configurations/TodoItemConfiguration.cs
  Todo.Infrastructure/Data/Migrations/
  Todo.Infrastructure/Data/TodoItemStore.cs
  Todo.API/Controllers/TodosController.cs
  Todo.API/Middleware/ExceptionHandlingMiddleware.cs
  Todo.API/Program.cs
  Todo.API/appsettings.json
  Todo.API/appsettings.Development.json
tests/Todo.Tests/Unit/
tests/Todo.Tests/Integration/
todo-web/src/app/core/api/
todo-web/src/app/features/todos/
todo-web/src/environments/
```

Folder names are a guide, not a reason to add empty layers or unnecessary files.

## Domain and database

One `TodoItem` maps to one `TodoItems` table. There are no relationships in this release.

| Property | Type | SQL constraint / behavior |
| --- | --- | --- |
| `Id` | `Guid` | Primary key, `uniqueidentifier`, generated in application |
| `Title` | `string` | Required, trimmed, 1–200 characters, `nvarchar(200)` |
| `Description` | `string?` | Optional, at most 2000 characters, `nvarchar(2000)` |
| `Priority` | `TodoPriority` | Required enum `Low`, `Normal`, `High`; stored as `tinyint` with defined numeric values |
| `DueAtUtc` | `DateTime?` | Optional, UTC, `datetime2`; past dates are allowed |
| `IsCompleted` | `bool` | Required, default false |
| `CompletedAtUtc` | `DateTime?` | UTC when completed; null when reopened |
| `CreatedAtUtc` | `DateTime` | Required UTC creation time |
| `UpdatedAtUtc` | `DateTime` | Required UTC last change time |

The domain owns title/description length and completion timestamp invariants. The application validates request shape and supported enum values before persistence. Use `IEntityTypeConfiguration<TodoItem>` for the table, field lengths, required fields, enum conversion, and an index on `(IsCompleted, CreatedAtUtc)` for filtered listing. Sort lists by `CreatedAtUtc` descending, then `Id` for stable ties. Avoid database-specific defaults for application timestamps.

## HTTP contract

Base route: `/api/todos`. JSON uses camelCase. `id` is a GUID. All timestamps use ISO 8601 UTC. `TodoResponse` contains all entity fields. `CreateTodoRequest` has required `title`, optional `description`, optional `priority` (default `Normal`), and optional `dueAtUtc`. `UpdateTodoRequest` has the same editable fields plus required `isCompleted`; PUT replaces the editable state, and omitted optional values clear them. Neither request accepts IDs or server-managed timestamps.

| Method and route | Behavior | Success | Errors |
| --- | --- | --- | --- |
| `GET /api/todos?isCompleted=true|false` | List all or filter by completion | `200` array | `400` for invalid filter |
| `GET /api/todos/{id}` | Read one | `200` response | `404` missing |
| `POST /api/todos` | Create | `201` response with `Location` | `400` validation |
| `PUT /api/todos/{id}` | Edit fields and complete/reopen | `200` response | `400` validation; `404` missing |
| `DELETE /api/todos/{id}` | Delete one | `204` | `404` missing |

Use ASP.NET Core request validation with clear field errors. Return `ProblemDetails` for client and server errors, including unexpected exceptions; never expose stack traces in production. Cancellation tokens flow from HTTP through application and EF Core. Document the contract with OpenAPI in development.

## Angular frontend

Use a feature folder for the todo list, editor form, and todo row/card; a typed API service and TypeScript request/response models live under `core/api`. The root page loads todos, filters all/active/completed, opens create/edit forms, toggles completion, and confirms deletion. Show loading, empty, validation, and API error states. Use Angular reactive forms with the same visible title and description limits; the server remains authoritative. Keep the API base URL in non-secret environment configuration. Provide accessible labels, keyboard-operable controls, and clear completion state. No state-management library is needed for this scope.

## Configuration and migration strategy

Register `TodoDbContext`, todo store, and application service via dependency injection. `appsettings.json` contains no connection details. `appsettings.Development.json` may hold only non-secret settings such as the Angular origin. Supply the SQL Server connection for the dedicated `BandTodoDb` development database via .NET User Secrets or an environment variable. Never commit credentials. Enable CORS only in development for the configured Angular origin.

Create the initial EF Core 9 Code First migration in `Todo.Infrastructure/Data/Migrations`; keep EF Core's default `__EFMigrationsHistory`. Review generated SQL and migration operations before applying. Apply migrations to the dedicated development database only after a valid connection is configured. Do not use `EnsureCreated`, `EnsureDeleted`, or ad-hoc table creation SQL. Future destructive schema or data changes require explicit human approval before execution.

## Milestones and ownership

1. **Architecture approval — Architect / owner.** Architect publishes this plan. Owner explicitly approves or requests changes. No implementation delegation before approval.
2. **Backend foundation — Developer.** Create solution/projects and references exactly along the dependency direction; implement domain, DTOs, validation, application service, infrastructure, configuration, and API. Developer owns application code and migrations.
3. **Frontend — Developer.** Build the Angular feature and typed HTTP integration against the approved contract. Keep within the single-user scope.
4. **Verification — QA.** QA owns tests and their execution report. Run unit tests for invariants/service behavior, API integration tests for routes/validation/errors, and Angular tests for the main interactions. Verify migration application and actual SQL Server persistence in `BandTodoDb`; record commands, environment, outcomes, and blockers. Do not claim database success from an in-memory provider.
5. **Independent review — Reviewer.** Review dependency direction, API behavior, validation, configuration/secrets, EF mapping/migration safety, frontend usability, and QA evidence. Report actionable findings and approval or remaining blockers.
6. **Corrections and completion — Developer, QA, Reviewer, Architect.** Developer fixes findings, QA reruns affected checks, Reviewer rechecks changed findings, and Architect reports final evidence to owner.

## Acceptance criteria

- Solution builds with .NET 9; Angular production build completes; no circular project references.
- A todo can be created, listed, read, edited, completed, reopened, filtered, and deleted through the UI and API; the state survives API restart in SQL Server.
- Invalid title, overlong description, unsupported priority, and malformed requests yield field-level `400` responses; missing IDs yield `404`; successful POST returns `201` with a usable `Location`.
- Domain invariants hold, including UTC completion timestamps and clearing `CompletedAtUtc` on reopen.
- The initial migration creates the expected table, constraints, and index in `BandTodoDb`; default `__EFMigrationsHistory` is preserved. No destructive database operation is performed without owner approval.
- Connection secrets are absent from tracked files; development CORS is limited to the configured Angular origin; unexpected failures return consistent non-sensitive errors.
- xUnit and Angular tests execute and pass; QA provides commands and results, including a real SQL Server migration/persistence check. Reviewer provides independent approval after any corrections.

## Approval gate

The owner explicitly approved this plan in the Band room on 2026-11-18. Phase 2 may proceed under the ownership, scope, and safety rules above.
