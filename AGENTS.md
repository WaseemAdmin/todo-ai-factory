# Todo AI Factory — Shared Engineering Rules

## Technology
- ASP.NET Core Web API (.NET 9)
- Angular with TypeScript
- SQL Server and EF Core 9
- xUnit

## Architecture
- Use Todo.API, Todo.Application, Todo.Domain,
  Todo.Infrastructure, and Todo.Tests.
- Keep controllers thin.
- Use dependency injection.
- Keep Domain independent of Infrastructure.
- Follow clean code and SOLID principles where appropriate.
- Avoid unnecessary complexity.

## Database
- Use EF Core Code First migrations.
- Use TodoDbContext.
- Use IEntityTypeConfiguration<T>.
- Use a dedicated BandTodoDb database.
- Preserve EF Core's __EFMigrationsHistory table.
- Never use EnsureCreated or EnsureDeleted.
- Never hardcode database credentials.
- Require approval for destructive database operations.

## Collaboration
- Architect owns planning and delegation.
- Developer owns implementation.
- QA owns testing.
- Reviewer owns code review.
- Use Band mentions for coordination.
- Do not claim success without executed verification.


## Agent Communication and Termination Rules
1. Mention another agent only when it needs to perform a new action, answer a question, or review new work.
2. Do not send acknowledgment-only messages such as:
   - Confirmed
   - OK
   - Thanks
   - Received
3. When an agent completes a requested task, it should send one concise result to the requesting agent.
4. When the requesting agent receives a complete result, it must not mention the responding agent again unless new work is required.
5. Do not repeatedly summarize information already reported.
6. Address final task summaries to the human user, without unnecessarily mentioning other agents.
7. If no additional action or reply is required, end the turn without sending another room message when the runtime supports that behavior.
8. Do not repeatedly execute the same Band CLI command after it has already succeeded.
9. If a tool requires human approval, request approval once and wait. Do not create duplicate permission requests for the same operation.
10. A completed task must have a clear stopping condition. After reaching it, stop.
11. Do not start implementation work until the human approves the architecture plan.


