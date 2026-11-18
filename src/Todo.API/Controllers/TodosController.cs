using Microsoft.AspNetCore.Mvc;
using Todo.Application.DTOs.Todos;
using Todo.Application.Services;

namespace Todo.API.Controllers;

[ApiController]
[Route("api/todos")]
public sealed class TodosController(TodoService todoService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TodoResponse>>> List([FromQuery] bool? isCompleted, CancellationToken cancellationToken) =>
        Ok(await todoService.ListAsync(isCompleted, cancellationToken));

    [HttpGet("{id}")]
    public async Task<ActionResult<TodoResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var todo = await todoService.GetAsync(id, cancellationToken);
        return todo is null ? Problem(statusCode: 404, title: "Todo not found") : Ok(todo);
    }

    [HttpPost]
    public async Task<ActionResult<TodoResponse>> Create([FromBody] CreateTodoRequest request, CancellationToken cancellationToken)
    {
        var todo = await todoService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = todo.Id }, todo);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TodoResponse>> Update(Guid id, [FromBody] UpdateTodoRequest request, CancellationToken cancellationToken)
    {
        var todo = await todoService.UpdateAsync(id, request, cancellationToken);
        return todo is null ? Problem(statusCode: 404, title: "Todo not found") : Ok(todo);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await todoService.DeleteAsync(id, cancellationToken) ? NoContent() : Problem(statusCode: 404, title: "Todo not found");
}
