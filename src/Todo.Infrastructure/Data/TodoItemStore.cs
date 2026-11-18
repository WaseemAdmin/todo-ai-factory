using Microsoft.EntityFrameworkCore;
using Todo.Application.Interfaces;
using Todo.Application.Services;
using Todo.Domain.Entities;

namespace Todo.Infrastructure.Data;

public sealed class TodoItemStore(TodoDbContext dbContext) : ITodoItemStore
{
    public async Task<IReadOnlyList<TodoItem>> ListAsync(bool? isCompleted, bool isCancelled, CancellationToken cancellationToken)
    {
        var query = dbContext.TodoItems.AsNoTracking().Where(item => item.IsCancelled == isCancelled);
        if (isCompleted.HasValue)
            query = query.Where(item => item.IsCompleted == isCompleted.Value);
        return await query.OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id).ToListAsync(cancellationToken);
    }

    public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.TodoItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task AddAsync(TodoItem item, CancellationToken cancellationToken) =>
        await dbContext.TodoItems.AddAsync(item, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Refresh the tracked row so a racing repeat cancellation can return 204.
            foreach (var entry in exception.Entries)
                await entry.ReloadAsync(cancellationToken);
            throw new TodoConflictException("Todo was changed concurrently.");
        }
    }
}
