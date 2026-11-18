# Todo Management

Single-user Todo application built with ASP.NET Core 9, EF Core 9, SQL Server, and Angular.

## Development setup

Use a dedicated SQL Server database named `BandTodoDb`. The API reads its connection from `ConnectionStrings__TodoDb` or .NET User Secrets. Keep credentials outside this repository. A LocalDB connection without a password is:

```text
Server=(localdb)\MSSQLLocalDB;Database=BandTodoDb;Trusted_Connection=True;TrustServerCertificate=True
```

The API project has a User Secrets ID. To use secrets, run `dotnet user-secrets set "ConnectionStrings:TodoDb" "<your BandTodoDb connection>" --project src/Todo.API` locally. For EF tooling, provide the same connection through the `ConnectionStrings__TodoDb` environment variable.

Apply the reviewed initial migration only to that database:

```text
dotnet ef database update --project src/Todo.Infrastructure --startup-project src/Todo.API
```

Start the API with `dotnet run --project src/Todo.API --launch-profile http`. Its development URL is `http://localhost:5000`, and its OpenAPI document is at `/openapi/v1.json`. Start Angular with `npm install` and `npm start` from `todo-web`; it serves at `http://localhost:4200`. The API allows that origin only in Development. The Angular API URL is configured in `todo-web/src/environments/environment.ts`.

Build with `dotnet build Todo.sln` and `npm run build` from `todo-web`.
