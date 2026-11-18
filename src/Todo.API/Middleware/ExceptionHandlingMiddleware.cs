using Microsoft.AspNetCore.Mvc;
using Todo.Application.Services;

namespace Todo.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (TodoValidationException exception)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ValidationProblemDetails(exception.Errors)
            {
                Status = 400, Title = "Validation failed"
            }, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; no response can be delivered.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request failure");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 500, Title = "An unexpected error occurred"
            }, context.RequestAborted);
        }
    }
}
