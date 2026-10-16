using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HabitSchedulesAndProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduleDaysMask",
                table: "Habits",
                type: "integer",
                nullable: false,
                defaultValue: 127);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "HabitCompletions",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduleDaysMask",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "HabitCompletions");
        }
    }
}
