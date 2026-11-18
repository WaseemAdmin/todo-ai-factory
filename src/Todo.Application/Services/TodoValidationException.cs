namespace Todo.Application.Services;

public sealed class TodoValidationException(IDictionary<string, string[]> errors) : Exception("Todo validation failed.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
