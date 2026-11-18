using Microsoft.EntityFrameworkCore;
using Todo.Application.Interfaces;
using Todo.Domain.Entities;

namespace Todo.Infrastructure.Data;

public sealed class TodoItemStore(TodoDbContext dbContext) : ITodoItemStore
{
    public async Task<IReadOnlyList<TodoItem>> ListAsync(bool? isCompleted, CancellationToken cancellationToken)
    {
        var query = dbContext.TodoItems.AsNoTracking().AsQueryable();
        if (isCompleted.HasValue)
            query = query.Where(item => item.IsCompleted == isCompleted.Value);
        return await query.OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id).ToListAsync(cancellationToken);
    }

    public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.TodoItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task AddAsync(TodoItem item, CancellationToken cancellationToken) =>
        await dbContext.TodoItems.AddAsync(item, cancellationToken);

    public void Remove(TodoItem item) => dbContext.TodoItems.Remove(item);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
