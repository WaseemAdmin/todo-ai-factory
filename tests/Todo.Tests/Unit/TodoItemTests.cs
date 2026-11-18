using Todo.Domain.Entities;
using Todo.Domain.Enums;

namespace Todo.Tests.Unit;

public sealed class TodoItemTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_TrimsTitleAndStartsOpenWithUtcTimestamps()
    {
        var item = TodoItem.Create("  buy milk  ", null, TodoPriority.Normal, null, Now);
        Assert.Equal("buy milk", item.Title);
        Assert.False(item.IsCompleted);
        Assert.Null(item.CompletedAtUtc);
        Assert.Equal(Now, item.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, item.CreatedAtUtc.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankTitle(string title) =>
        Assert.Throws<ArgumentException>(() => TodoItem.Create(title, null, TodoPriority.Normal, null, Now));

    [Fact]
    public void Create_RejectsOverlongTitleAndDescription()
    {
        Assert.Throws<ArgumentException>(() => TodoItem.Create(new string('x', 201), null, TodoPriority.Normal, null, Now));
        Assert.Throws<ArgumentException>(() => TodoItem.Create("valid", new string('x', 2001), TodoPriority.Normal, null, Now));
    }

    [Fact]
    public void Update_CompletesAndReopensWithUtcTimestamp()
    {
        var item = TodoItem.Create("task", null, TodoPriority.Normal, null, Now);
        item.Update("updated", null, TodoPriority.High, null, true, Now.AddMinutes(1));
        Assert.True(item.IsCompleted);
        Assert.Equal(Now.AddMinutes(1), item.CompletedAtUtc);
        Assert.Equal(DateTimeKind.Utc, item.CompletedAtUtc!.Value.Kind);
        item.Update("updated", null, TodoPriority.High, null, false, Now.AddMinutes(2));
        Assert.False(item.IsCompleted);
        Assert.Null(item.CompletedAtUtc);
        Assert.Equal(Now.AddMinutes(2), item.UpdatedAtUtc);
    }

    [Fact]
    public void Create_RejectsNonUtcDates() =>
        Assert.Throws<ArgumentException>(() => TodoItem.Create("valid", null, TodoPriority.Normal, null, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
}
