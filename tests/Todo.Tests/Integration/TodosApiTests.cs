using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Todo.Application.Interfaces;
using Todo.Domain.Entities;
using Todo.Domain.Enums;

namespace Todo.Tests.Integration;

public sealed class TodosApiTests : IClassFixture<TodosApiFactory>
{
    private readonly TodosApiFactory factory;
    private readonly HttpClient client;

    public TodosApiTests(TodosApiFactory factory)
    {
        this.factory = factory;
        factory.Store.Clear();
        client = factory.CreateClient();
    }

    [Fact]
    public async Task CrudAndFiltering_UsesExpectedStatusCodesAndCancelsOnlyTarget()
    {
        var first = await client.PostAsJsonAsync("/api/todos", new { title = " first ", priority = "High" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstTodo = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.EndsWith(firstTodo.ToString(), first.Headers.Location?.ToString());
        var second = await client.PostAsJsonAsync("/api/todos", new { title = "second" });
        var secondTodo = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var get = await client.GetFromJsonAsync<JsonElement>($"/api/todos/{firstTodo}");
        Assert.Equal("first", get.GetProperty("title").GetString());

        var update = await client.PutAsJsonAsync($"/api/todos/{firstTodo}", new { title = "edited", isCompleted = true });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var completed = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(completed.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, completed.GetProperty("completedAtUtc").ValueKind);
        var filtered = await client.GetFromJsonAsync<JsonElement>("/api/todos?isCompleted=true");
        Assert.Single(filtered.EnumerateArray());

        var reopen = await client.PutAsJsonAsync($"/api/todos/{firstTodo}", new { title = "edited", isCompleted = false });
        var reopened = await reopen.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("completedAtUtc").ValueKind);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/todos/{firstTodo}")).StatusCode);
        var cancelled = await client.GetFromJsonAsync<JsonElement>($"/api/todos/{firstTodo}");
        Assert.True(cancelled.GetProperty("isCancelled").GetBoolean());
        Assert.EndsWith("Z", cancelled.GetProperty("cancelledAtUtc").GetString());
        var active = await client.GetFromJsonAsync<JsonElement>("/api/todos?isCompleted=false");
        Assert.DoesNotContain(active.EnumerateArray(), row => row.GetProperty("id").GetGuid() == firstTodo);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/todos/{secondTodo}")).StatusCode);
    }

    [Fact]
    public async Task Delete_CancelsIdempotentlyPreservesFieldsAndFiltersFromActiveViews()
    {
        var due = "2026-12-01T12:00:00Z";
        var create = await client.PostAsJsonAsync("/api/todos", new
        {
            title = "preserve me", description = "important", priority = "High", dueAtUtc = due
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var complete = await client.PutAsJsonAsync($"/api/todos/{id}", new
        {
            title = "preserve me", description = "important", priority = "High", dueAtUtc = due, isCompleted = true
        });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completedAt = (await complete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("completedAtUtc").GetString();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/todos/{id}")).StatusCode);
        var firstRead = await client.GetFromJsonAsync<JsonElement>($"/api/todos/{id}");
        Assert.True(firstRead.GetProperty("isCancelled").GetBoolean());
        var cancelledAt = firstRead.GetProperty("cancelledAtUtc").GetString();
        Assert.EndsWith("Z", cancelledAt);
        Assert.Equal("preserve me", firstRead.GetProperty("title").GetString());
        Assert.Equal("important", firstRead.GetProperty("description").GetString());
        Assert.Equal("High", firstRead.GetProperty("priority").GetString());
        Assert.Equal(due, firstRead.GetProperty("dueAtUtc").GetString());
        Assert.True(firstRead.GetProperty("isCompleted").GetBoolean());
        Assert.Equal(completedAt, firstRead.GetProperty("completedAtUtc").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/todos/{id}")).StatusCode);
        var secondRead = await client.GetFromJsonAsync<JsonElement>($"/api/todos/{id}");
        Assert.Equal(cancelledAt, secondRead.GetProperty("cancelledAtUtc").GetString());
        Assert.Equal(firstRead.GetProperty("updatedAtUtc").GetString(), secondRead.GetProperty("updatedAtUtc").GetString());
        foreach (var route in new[] { "/api/todos", "/api/todos?isCompleted=true", "/api/todos?isCancelled=false" })
        {
            var visible = await client.GetFromJsonAsync<JsonElement>(route);
            Assert.DoesNotContain(visible.EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        }
        var cancelledList = await client.GetFromJsonAsync<JsonElement>("/api/todos?isCancelled=true");
        Assert.Contains(cancelledList.EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        var edit = await client.PutAsJsonAsync($"/api/todos/{id}", new { title = "changed", isCompleted = false });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/todos?isCancelled=true&isCompleted=true")).StatusCode);
    }

    [Theory]
    [InlineData("{}", "title")]
    [InlineData("{\"title\":\"\"}", "title")]
    [InlineData("{\"title\":\"   \"}", "title")]
    [InlineData("{\"title\":\"valid\",\"description\":\"TOO_LONG\"}", "description")]
    [InlineData("{\"title\":\"valid\",\"priority\":\"Urgent\"}", "priority")]
    public async Task Post_InvalidFieldsReturnBadRequest(string body, string field)
    {
        body = body.Replace("TOO_LONG", new string('x', 2001));
        var response = await client.PostAsync("/api/todos", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(field, content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingIdsAndMalformedFilter_ReturnExpectedErrors()
    {
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/todos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/todos/{id}", new { title = "valid", isCompleted = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/todos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/todos?isCompleted=maybe")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/todos?isCancelled=maybe")).StatusCode);
    }

    [Fact]
    public async Task Put_RequiresCompletionState()
    {
        var response = await client.PutAsJsonAsync($"/api/todos/{Guid.NewGuid()}", new { title = "valid" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("isCompleted", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_MalformedJsonReturnsBadRequest()
    {
        var response = await client.PostAsync("/api/todos", new StringContent("{broken", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_AcceptsTitleWithTwoHundredCharactersAfterTrimming()
    {
        var title = "  " + new string('x', 200) + "  ";
        var response = await client.PostAsJsonAsync("/api/todos", new { title });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new string('x', 200), todo.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Post_RejectsTitleWithTwoHundredOneCharactersAfterTrimming()
    {
        var response = await client.PostAsJsonAsync("/api/todos", new { title = "  " + new string('x', 201) + "  " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("title", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestFactories_KeepIndependentConnectionConfiguration()
    {
        using var sqlFactory = new SqlServerApiFactory();
        var fakeConnection = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("TodoDb");
        var sqlConnection = sqlFactory.Services.GetRequiredService<IConfiguration>().GetConnectionString("TodoDb");
        Assert.Equal("Server=localhost;Database=unused;Trusted_Connection=True", fakeConnection);
        Assert.Equal(SqlServerPersistenceTests.Connection, sqlConnection);
    }
}

public sealed class TodosApiFactory : WebApplicationFactory<Program>
{
    public InMemoryTodoStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:TodoDb", "Server=localhost;Database=unused;Trusted_Connection=True");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:TodoDb"] = "Server=localhost;Database=unused;Trusted_Connection=True",
            ["AngularOrigin"] = "http://localhost:4200"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITodoItemStore>();
            services.AddSingleton<ITodoItemStore>(Store);
        });
    }
}

public sealed class InMemoryTodoStore : ITodoItemStore
{
    private readonly Dictionary<Guid, TodoItem> items = new();
    public void Clear() => items.Clear();
    public Task<IReadOnlyList<TodoItem>> ListAsync(bool? isCompleted, bool isCancelled, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TodoItem>>(items.Values.Where(x => x.IsCancelled == isCancelled &&
            (isCompleted is null || x.IsCompleted == isCompleted))
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToArray());
    public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(id));
    public Task AddAsync(TodoItem item, CancellationToken cancellationToken) { items.Add(item.Id, item); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
