using Todo.Domain.Entities;

namespace Todo.Application.Interfaces;

public interface ITodoItemStore
{
    Task<IReadOnlyList<TodoItem>> ListAsync(bool? isCompleted, bool isCancelled, CancellationToken cancellationToken);
    Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(TodoItem item, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
