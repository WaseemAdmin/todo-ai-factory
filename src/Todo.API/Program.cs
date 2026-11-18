using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Todo.API.Middleware;
using Todo.Application.Interfaces;
using Todo.Application.Services;
using Todo.Domain.Enums;
using Todo.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<TodoPriority>(allowIntegerValues: false)));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TodoService>();
builder.Services.AddScoped<ITodoItemStore, TodoItemStore>();

var connectionString = builder.Configuration.GetConnectionString("TodoDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Set ConnectionStrings__TodoDb to the dedicated BandTodoDb database.");
builder.Services.AddDbContext<TodoDbContext>(options => options.UseSqlServer(connectionString));

if (builder.Environment.IsDevelopment())
{
    var angularOrigin = builder.Configuration["AngularOrigin"]
        ?? throw new InvalidOperationException("Configure AngularOrigin for development CORS.");
    builder.Services.AddCors(options => options.AddPolicy("AngularDevelopment", policy =>
        policy.WithOrigins(angularOrigin).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("AngularDevelopment");
}
app.MapControllers();
app.Run();

public partial class Program;
