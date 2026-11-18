using Todo.Domain.Enums;

namespace Todo.Domain.Entities;

public sealed class TodoItem
{
    private TodoItem() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TodoPriority Priority { get; private set; }
    public DateTime? DueAtUtc { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static TodoItem Create(string title, string? description, TodoPriority priority, DateTime? dueAtUtc, DateTime nowUtc)
    {
        Validate(title, description, priority, dueAtUtc, nowUtc);
        return new TodoItem
        {
            Id = Guid.NewGuid(), Title = title.Trim(), Description = description,
            Priority = priority, DueAtUtc = dueAtUtc, CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc
        };
    }

    public void Update(string title, string? description, TodoPriority priority, DateTime? dueAtUtc, bool isCompleted, DateTime nowUtc)
    {
        Validate(title, description, priority, dueAtUtc, nowUtc);
        Title = title.Trim();
        Description = description;
        Priority = priority;
        DueAtUtc = dueAtUtc;
        if (isCompleted != IsCompleted)
        {
            IsCompleted = isCompleted;
            CompletedAtUtc = isCompleted ? nowUtc : null;
        }
        UpdatedAtUtc = nowUtc;
    }

    private static void Validate(string title, string? description, TodoPriority priority, DateTime? dueAtUtc, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ArgumentException("Title must contain 1 to 200 characters.", nameof(title));
        if (description?.Length > 2000)
            throw new ArgumentException("Description must be at most 2000 characters.", nameof(description));
        if (!Enum.IsDefined(priority))
            throw new ArgumentOutOfRangeException(nameof(priority));
        if (nowUtc.Kind != DateTimeKind.Utc || dueAtUtc is { Kind: not DateTimeKind.Utc })
            throw new ArgumentException("Dates must be UTC.");
    }
}
