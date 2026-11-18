using System.ComponentModel.DataAnnotations;
using Todo.Domain.Enums;

namespace Todo.Application.DTOs.Todos;

public class TodoWriteRequest
{
    [Required]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public TodoPriority? Priority { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
}

public sealed class CreateTodoRequest : TodoWriteRequest;

public sealed class UpdateTodoRequest : TodoWriteRequest
{
    [Required]
    public bool? IsCompleted { get; set; }
}

public sealed record TodoResponse(
    Guid Id, string Title, string? Description, TodoPriority Priority, DateTime? DueAtUtc,
    bool IsCompleted, DateTime? CompletedAtUtc, bool IsCancelled, DateTime? CancelledAtUtc,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
