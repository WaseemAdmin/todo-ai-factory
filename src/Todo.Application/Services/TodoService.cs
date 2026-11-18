using Todo.Application.DTOs.Todos;
using Todo.Application.Interfaces;
using Todo.Domain.Entities;
using Todo.Domain.Enums;

namespace Todo.Application.Services;

public sealed class TodoService(ITodoItemStore store, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<TodoResponse>> ListAsync(bool? isCompleted, CancellationToken cancellationToken)
    {
        var items = await store.ListAsync(isCompleted, cancellationToken);
        return items.Select(ToResponse).ToArray();
    }

    public async Task<TodoResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(id, cancellationToken);
        return item is null ? null : ToResponse(item);
    }

    public async Task<TodoResponse> CreateAsync(CreateTodoRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Title, request.Description, request.Priority);
        var item = TodoItem.Create(request.Title!, request.Description, request.Priority ?? TodoPriority.Normal,
            request.DueAtUtc?.UtcDateTime, timeProvider.GetUtcNow().UtcDateTime);
        await store.AddAsync(item, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    public async Task<TodoResponse?> UpdateAsync(Guid id, UpdateTodoRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Title, request.Description, request.Priority, request.IsCompleted);
        var item = await store.GetAsync(id, cancellationToken);
        if (item is null) return null;
        item.Update(request.Title!, request.Description, request.Priority ?? TodoPriority.Normal,
            request.DueAtUtc?.UtcDateTime, request.IsCompleted!.Value, timeProvider.GetUtcNow().UtcDateTime);
        await store.SaveChangesAsync(cancellationToken);
        return ToResponse(item);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(id, cancellationToken);
        if (item is null) return false;
        store.Remove(item);
        await store.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static TodoResponse ToResponse(TodoItem item) => new(
        item.Id, item.Title, item.Description, item.Priority, item.DueAtUtc, item.IsCompleted,
        item.CompletedAtUtc, item.CreatedAtUtc, item.UpdatedAtUtc);

    private static void Validate(string? title, string? description, TodoPriority? priority, bool? isCompleted = true)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            errors["title"] = ["Title must contain 1 to 200 characters."];
        if (description?.Length > 2000)
            errors["description"] = ["Description must be at most 2000 characters."];
        if (priority is not null && !Enum.IsDefined(priority.Value))
            errors["priority"] = ["Priority must be Low, Normal, or High."];
        if (isCompleted is null)
            errors["isCompleted"] = ["Completion state is required."];
        if (errors.Count > 0) throw new TodoValidationException(errors);
    }
}
