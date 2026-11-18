namespace Todo.Application.Services;

public sealed class TodoConflictException(string message) : Exception(message);
