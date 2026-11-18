# Todo AI Factory

A single-user Todo Management app built as a hands-on exercise with [Band AI](https://band.ai/). Create and organize todos in an Angular interface backed by an ASP.NET Core API and SQL Server. Todos are stored in the database, so they remain available after an API restart.

This is our team's first project created with Band AI. Four agents completed it together: **Architect** planned the system, **Developer** built the application, **QA** tested it, and **Reviewer** reviewed the result. The project was a training exercise for learning how to use Band AI to coordinate a small software team.

## What the app does

- Create, list, view, edit, and delete todos.
- Mark todos complete or reopen them, and filter the list by all, active, or completed.
- Set a priority (`Low`, `Normal`, or `High`) and an optional due date and description.
- Show loading, empty, validation, and API error states in a responsive interface.
- Persist records through the API in SQL Server. Creation, update, and completion timestamps use UTC.

## Technology

| Area | Stack |
| --- | --- |
| API | C# and ASP.NET Core Web API on .NET 9 |
| Frontend | Angular 21 and TypeScript |
| Persistence | SQL Server, Entity Framework Core 9, Code First migrations |
| Tests | xUnit for .NET; Vitest for Angular |

## Architecture

The API separates the Todo rules from HTTP and database code. Project references point in one direction:

| Project | Responsibility |
| --- | --- |
| `src/Todo.Domain` | Todo entity, priority enum, and entity invariants; no persistence dependency |
| `src/Todo.Application` | Request/response DTOs, validation, Todo service, and persistence interface |
| `src/Todo.Infrastructure` | `TodoDbContext`, SQL Server mapping, the Todo store, and EF Core migrations |
| `src/Todo.API` | HTTP routes, dependency injection, development CORS, OpenAPI, and error responses |
| `todo-web` | Angular UI and typed HTTP client |
| `tests/Todo.Tests` | xUnit unit, API, and SQL Server integration tests |

The main HTTP contract is `GET /api/todos`, `GET /api/todos/{id}`, `POST /api/todos`, `PUT /api/todos/{id}`, and `DELETE /api/todos/{id}`. The list endpoint accepts an optional `isCompleted=true|false` filter. The API returns field-level validation errors and ProblemDetails responses. The development OpenAPI document is available at `http://localhost:5000/openapi/v1.json`.

## Run locally

You need the .NET 9 SDK, a Node.js version supported by Angular 21 (this project was built with Node.js 22), npm, SQL Server or SQL Server LocalDB, and the `dotnet ef` CLI for migrations.

1. Clone the repository and restore .NET packages:

   ```powershell
   dotnet restore Todo.sln
   ```

2. Point the API and EF tooling to a dedicated development database named `BandTodoDb`. For Windows LocalDB, this connection uses Windows authentication and contains no password:

   ```powershell
   $env:ConnectionStrings__TodoDb = 'Server=(localdb)\MSSQLLocalDB;Database=BandTodoDb;Trusted_Connection=True;TrustServerCertificate=True'
   ```

   Set the environment variable in the terminal that will run the API or EF commands. For another SQL Server instance, use a connection string for your own `BandTodoDb` and keep credentials outside Git. The API also supports .NET User Secrets: `dotnet user-secrets set "ConnectionStrings:TodoDb" "<your connection string>" --project src/Todo.API`. The EF design-time factory reads `ConnectionStrings__TodoDb` from the environment.

3. Apply the initial migration to **BandTodoDb** and start the API from the repository root:

   ```powershell
   dotnet ef database update --project src/Todo.Infrastructure --startup-project src/Todo.API
   dotnet run --project src/Todo.API --launch-profile http
   ```

   The API listens at `http://localhost:5000`. Migrations are not applied automatically at startup.

4. In another terminal, install and start Angular:

   ```powershell
   cd todo-web
   npm ci
   npm start
   ```

   Open `http://localhost:4200`. Development CORS allows that origin only. The frontend API URL is set in `todo-web/src/environments/environment.ts`.

## Build and test

From the repository root:

```powershell
dotnet build Todo.sln
dotnet test Todo.sln
```

The SQL Server integration tests use a separate Windows LocalDB database named `BandTodoTestDb`; they need a working `MSSQLLocalDB` instance. They apply the EF migration to that test database and exercise API and SQL Server persistence. They do not use `BandTodoDb`.

From `todo-web`:

```powershell
npm ci
npm run build
npm test -- --watch=false
```

The Angular tests use Vitest with one worker to run reliably on Windows.
