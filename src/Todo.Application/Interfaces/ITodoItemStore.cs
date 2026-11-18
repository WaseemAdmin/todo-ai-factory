using Todo.Domain.Entities;

namespace Todo.Application.Interfaces;

public interface ITodoItemStore
{
    Task<IReadOnlyList<TodoItem>> ListAsync(bool? isCompleted, CancellationToken cancellationToken);
    Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(TodoItem item, CancellationToken cancellationToken);
    void Remove(TodoItem item);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
