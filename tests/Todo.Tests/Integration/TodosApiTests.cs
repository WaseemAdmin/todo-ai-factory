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
    public async Task CrudAndFiltering_UsesExpectedStatusCodesAndDeletesOnlyTarget()
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
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/todos/{firstTodo}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/todos/{secondTodo}")).StatusCode);
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
}

public sealed class TodosApiFactory : WebApplicationFactory<Program>
{
    public InMemoryTodoStore Store { get; } = new();

    public TodosApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__TodoDb", "Server=localhost;Database=unused;Trusted_Connection=True");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
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
    public Task<IReadOnlyList<TodoItem>> ListAsync(bool? isCompleted, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TodoItem>>(items.Values.Where(x => isCompleted is null || x.IsCompleted == isCompleted)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToArray());
    public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(id));
    public Task AddAsync(TodoItem item, CancellationToken cancellationToken) { items.Add(item.Id, item); return Task.CompletedTask; }
    public void Remove(TodoItem item) => items.Remove(item.Id);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
