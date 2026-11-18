using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Todo.Infrastructure.Data;

public sealed class TodoDbContextFactory : IDesignTimeDbContextFactory<TodoDbContext>
{
    public TodoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__TodoDb")
            ?? throw new InvalidOperationException("Set ConnectionStrings__TodoDb for EF Core tooling.");
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseSqlServer(connectionString).Options;
        return new TodoDbContext(options);
    }
}
