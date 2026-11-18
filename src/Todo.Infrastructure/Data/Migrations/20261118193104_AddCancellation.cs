using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Todo.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "TodoItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "TodoItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_IsCancelled_CreatedAtUtc",
                table: "TodoItems",
                columns: new[] { "IsCancelled", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TodoItems_IsCancelled_CreatedAtUtc",
                table: "TodoItems");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "TodoItems");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "TodoItems");
        }
    }
}
