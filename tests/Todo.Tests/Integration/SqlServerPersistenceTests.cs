using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Todo.Domain.Entities;
using Todo.Domain.Enums;
using Todo.Infrastructure.Data;

namespace Todo.Tests.Integration;

public sealed class SqlServerPersistenceTests
{
    internal const string Connection = "Server=(localdb)\\MSSQLLocalDB;Database=BandTodoTestDb;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public async Task InitialMigrationAndCrud_PersistAcrossContextsWithUtcDates()
    {
        var options = new DbContextOptionsBuilder<TodoDbContext>().UseSqlServer(Connection).Options;
        var now = new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);
        var first = TodoItem.Create("QA first", "persistence", TodoPriority.High, now.AddDays(1), now);
        var second = TodoItem.Create("QA second", null, TodoPriority.Normal, null, now);

        await using (var db = new TodoDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using (var sql = new SqlConnection(Connection))
        {
            await sql.OpenAsync();
            await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20261117225332_InitialCreate'; SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.TodoItems') AND name='IX_TodoItems_IsCompleted_CreatedAtUtc';", sql);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.True(await reader.NextResultAsync());
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1, reader.GetInt32(0));
        }

        try
        {
            await using (var db = new TodoDbContext(options))
            {
                db.TodoItems.AddRange(first, second);
                await db.SaveChangesAsync();
            }

            await using (var db = new TodoDbContext(options))
            {
                var loaded = await db.TodoItems.SingleAsync(x => x.Id == first.Id);
                Assert.Equal("QA first", loaded.Title);
                Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);
                Assert.Equal(DateTimeKind.Utc, loaded.DueAtUtc!.Value.Kind);
                loaded.Update("QA edited", null, TodoPriority.Low, null, true, now.AddMinutes(1));
                await db.SaveChangesAsync();
            }

            await using (var db = new TodoDbContext(options))
            {
                var loaded = await db.TodoItems.SingleAsync(x => x.Id == first.Id);
                Assert.Equal("QA edited", loaded.Title);
                Assert.True(loaded.IsCompleted);
                Assert.Equal(DateTimeKind.Utc, loaded.CompletedAtUtc!.Value.Kind);
                Assert.Equal(now.AddMinutes(1), loaded.UpdatedAtUtc);
                loaded.Update(loaded.Title, null, loaded.Priority, null, false, now.AddMinutes(2));
                db.TodoItems.Remove(loaded);
                await db.SaveChangesAsync();
            }

            await using (var db = new TodoDbContext(options))
            {
                Assert.Null(await db.TodoItems.SingleOrDefaultAsync(x => x.Id == first.Id));
                Assert.NotNull(await db.TodoItems.SingleOrDefaultAsync(x => x.Id == second.Id));
            }
        }
        finally
        {
            await using var cleanup = new TodoDbContext(options);
            await cleanup.TodoItems.Where(x => x.Id == first.Id || x.Id == second.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task ApiCrud_PersistsInSqlServerAndReturnsUtcAfterReload()
    {
        var options = new DbContextOptionsBuilder<TodoDbContext>().UseSqlServer(Connection).Options;
        await using (var db = new TodoDbContext(options)) await db.Database.MigrateAsync();
        using var factory = new SqlServerApiFactory();
        using var client = factory.CreateClient();
        Guid firstId = Guid.Empty;
        Guid secondId = Guid.Empty;
        try
        {
            var first = await client.PostAsJsonAsync("/api/todos", new { title = "QA API first", dueAtUtc = "2026-11-20T12:00:00Z" });
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var second = await client.PostAsJsonAsync("/api/todos", new { title = "QA API second" });
            secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var update = await client.PutAsJsonAsync($"/api/todos/{firstId}", new { title = "QA API updated", isCompleted = true });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);

            var read = await client.GetFromJsonAsync<JsonElement>($"/api/todos/{firstId}");
            Assert.Equal("QA API updated", read.GetProperty("title").GetString());
            Assert.True(read.GetProperty("isCompleted").GetBoolean());
            Assert.EndsWith("Z", read.GetProperty("completedAtUtc").GetString());
            var completed = await client.GetFromJsonAsync<JsonElement>("/api/todos?isCompleted=true");
            Assert.Contains(completed.EnumerateArray(), row => row.GetProperty("id").GetGuid() == firstId);
            await using (var db = new TodoDbContext(options))
            {
                var persisted = await db.TodoItems.SingleAsync(row => row.Id == firstId);
                Assert.Equal(DateTimeKind.Utc, persisted.CompletedAtUtc!.Value.Kind);
                Assert.Equal(DateTimeKind.Utc, persisted.CreatedAtUtc.Kind);
            }

            var reopen = await client.PutAsJsonAsync($"/api/todos/{firstId}", new { title = "QA API updated", isCompleted = false });
            Assert.Equal(JsonValueKind.Null, (await reopen.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("completedAtUtc").ValueKind);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/todos/{firstId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/todos/{firstId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/todos/{secondId}")).StatusCode);
        }
        finally
        {
            await using var cleanup = new TodoDbContext(options);
            await cleanup.TodoItems.Where(row => row.Id == firstId || row.Id == secondId).ExecuteDeleteAsync();
        }
    }
}

public sealed class SqlServerApiFactory : WebApplicationFactory<Program>
{
    public SqlServerApiFactory() => Environment.SetEnvironmentVariable("ConnectionStrings__TodoDb", SqlServerPersistenceTests.Connection);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AngularOrigin"] = "http://localhost:4200"
        }));
    }
}
