using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Todo.Domain.Entities;

namespace Todo.Infrastructure.Data.Configurations;

public sealed class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        // SQL Server datetime2 does not retain DateTime.Kind. Mark values as UTC when materializing.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        builder.ToTable("TodoItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
        builder.Property(x => x.Title).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnType("nvarchar(2000)").HasMaxLength(2000);
        builder.Property(x => x.Priority).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(x => x.DueAtUtc).HasConversion(utcConverter).HasColumnType("datetime2");
        builder.Property(x => x.IsCompleted).HasColumnType("bit").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.CompletedAtUtc).HasConversion(utcConverter).HasColumnType("datetime2");
        builder.Property(x => x.IsCancelled).HasColumnType("bit").HasDefaultValue(false).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.CancelledAtUtc).HasConversion(utcConverter).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAtUtc).HasConversion(utcConverter).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasConversion(utcConverter).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(x => new { x.IsCompleted, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.IsCancelled, x.CreatedAtUtc });
    }
}
