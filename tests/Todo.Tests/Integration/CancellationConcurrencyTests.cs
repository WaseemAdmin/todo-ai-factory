using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Todo.Application.Interfaces;
using Todo.Domain.Entities;
using Todo.Infrastructure.Data;

namespace Todo.Tests.Integration;

public sealed class CancellationConcurrencyTests
{
    [Fact]
    public async Task StalePutAfterDelete_ReturnsConflictAndPreservesFirstCancellation()
    {
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseSqlServer(SqlServerPersistenceTests.Connection).Options;
        await using (var db = new TodoDbContext(options)) await db.Database.MigrateAsync();

        using var factory = new GatedSqlServerApiFactory();
        using var client = factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/todos", new { title = "QA race " + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var originalTitle = created.GetProperty("title").GetString();

        factory.Gate.Arm(id);
        try
        {
            var putTask = client.PutAsJsonAsync($"/api/todos/{id}", new { title = "stale overwrite", isCompleted = true });
            await factory.Gate.Entered.WaitAsync(TimeSpan.FromSeconds(15));

            var firstDelete = await client.DeleteAsync($"/api/todos/{id}").WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(HttpStatusCode.NoContent, firstDelete.StatusCode);
            var firstRead = await client.GetFromJsonAsync<JsonElement>($"/api/todos/{id}");
            var firstCancelledAt = firstRead.GetProperty("cancelledAtUtc").GetString();
            var firstUpdatedAt = firstRead.GetProperty("updatedAtUtc").GetString();
            Assert.True(firstRead.GetProperty("isCancelled").GetBoolean());
            Assert.EndsWith("Z", firstCancelledAt);

            factory.Gate.Release();
            var stalePut = await putTask.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(HttpStatusCode.Conflict, stalePut.StatusCode);

            var repeatDelete = await client.DeleteAsync($"/api/todos/{id}");
            Assert.Equal(HttpStatusCode.NoContent, repeatDelete.StatusCode);
            var finalRead = await client.GetFromJsonAsync<JsonElement>($"/api/todos/{id}");
            Assert.Equal(originalTitle, finalRead.GetProperty("title").GetString());
            Assert.False(finalRead.GetProperty("isCompleted").GetBoolean());
            Assert.True(finalRead.GetProperty("isCancelled").GetBoolean());
            Assert.Equal(firstCancelledAt, finalRead.GetProperty("cancelledAtUtc").GetString());
            Assert.Equal(firstUpdatedAt, finalRead.GetProperty("updatedAtUtc").GetString());

            await using var verify = new TodoDbContext(options);
            var rows = await verify.TodoItems.AsNoTracking().Where(row => row.Id == id).ToListAsync();
            var row = Assert.Single(rows);
            Assert.True(row.IsCancelled);
            Assert.Equal(originalTitle, row.Title);
            Assert.Equal(DateTimeOffset.Parse(firstCancelledAt!), new DateTimeOffset(row.CancelledAtUtc!.Value));
        }
        finally
        {
            factory.Gate.Release();
            // This uniquely owned QA row is retained and only soft-cancelled.
            await client.DeleteAsync($"/api/todos/{id}");
        }
    }

    [Fact]
    public async Task ConcurrentDeletes_ReturnNoContentAndKeepFirstTimestampAndRow()
    {
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseSqlServer(SqlServerPersistenceTests.Connection).Options;
        await using (var db = new TodoDbContext(options)) await db.Database.MigrateAsync();

        using var factory = new GatedSqlServerApiFactory();
        using var client = factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/todos", new { title = "QA parallel delete " + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var originalTitle = created.GetProperty("title").GetString();

        factory.Gate.Arm(id);
        try
        {
            var staleDeleteTask = client.DeleteAsync($"/api/todos/{id}");
            await factory.Gate.Entered.WaitAsync(TimeSpan.FromSeconds(15));
            var firstDelete = await client.DeleteAsync($"/api/todos/{id}").WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(HttpStatusCode.NoContent, firstDelete.StatusCode);
            var firstRead = await client.GetFromJsonAsync<JsonElement>($"/api/todos/{id}");
            var firstCancelledAt = firstRead.GetProperty("cancelledAtUtc").GetString();
            var firstUpdatedAt = firstRead.GetProperty("updatedAtUtc").GetString();
            Assert.EndsWith("Z", firstCancelledAt);

            factory.Gate.Release();
            var staleDelete = await staleDeleteTask.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(HttpStatusCode.NoContent, staleDelete.StatusCode);
            var finalRead = await client.GetFromJsonAsync<JsonElement>($"/api/todos/{id}");
            Assert.Equal(firstCancelledAt, finalRead.GetProperty("cancelledAtUtc").GetString());
            Assert.Equal(firstUpdatedAt, finalRead.GetProperty("updatedAtUtc").GetString());
            Assert.Equal(originalTitle, finalRead.GetProperty("title").GetString());

            await using var verify = new TodoDbContext(options);
            var row = Assert.Single(await verify.TodoItems.AsNoTracking().Where(todo => todo.Id == id).ToListAsync());
            Assert.True(row.IsCancelled);
            Assert.Equal(originalTitle, row.Title);
            Assert.Equal(DateTimeOffset.Parse(firstCancelledAt!), new DateTimeOffset(row.CancelledAtUtc!.Value));
        }
        finally
        {
            factory.Gate.Release();
            await client.DeleteAsync($"/api/todos/{id}");
        }
    }
}

internal sealed class GatedSqlServerApiFactory : WebApplicationFactory<Program>
{
    public TodoReadGate Gate { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:TodoDb", SqlServerPersistenceTests.Connection);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:TodoDb"] = SqlServerPersistenceTests.Connection,
            ["AngularOrigin"] = "http://localhost:4200"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITodoItemStore>();
            services.AddScoped<ITodoItemStore>(sp => new GatedTodoItemStore(
                new TodoItemStore(sp.GetRequiredService<TodoDbContext>()), Gate));
        });
    }
}

internal sealed class TodoReadGate
{
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Guid target;
    private int captured;

    public Task Entered => entered.Task;
    public void Arm(Guid id) => target = id;
    public void Release() => released.TrySetResult();

    public async Task PauseFirstReadAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id != target || Interlocked.CompareExchange(ref captured, 1, 0) != 0) return;
        entered.TrySetResult();
        await released.Task.WaitAsync(cancellationToken);
    }
}

internal sealed class GatedTodoItemStore(ITodoItemStore inner, TodoReadGate gate) : ITodoItemStore
{
    public Task<IReadOnlyList<TodoItem>> ListAsync(bool? isCompleted, bool isCancelled, CancellationToken cancellationToken) =>
        inner.ListAsync(isCompleted, isCancelled, cancellationToken);

    public async Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await inner.GetAsync(id, cancellationToken);
        if (item is not null) await gate.PauseFirstReadAsync(id, cancellationToken);
        return item;
    }

    public Task AddAsync(TodoItem item, CancellationToken cancellationToken) => inner.AddAsync(item, cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => inner.SaveChangesAsync(cancellationToken);
}
