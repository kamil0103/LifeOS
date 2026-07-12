using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodingTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodingProblems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Difficulty = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsSolved = table.Column<bool>(type: "boolean", nullable: false),
                    SolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SolutionLanguage = table.Column<string>(type: "text", nullable: true),
                    TimeSpentMinutes = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodingProblems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodingProblems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProblemAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SolutionLanguage = table.Column<string>(type: "text", nullable: true),
                    TimeSpentMinutes = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    XpEarned = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProblemAttempts_CodingProblems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "CodingProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodingProblems_UserId",
                table: "CodingProblems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemAttempts_ProblemId",
                table: "ProblemAttempts",
                column: "ProblemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProblemAttempts");

            migrationBuilder.DropTable(
                name: "CodingProblems");
        }
    }
}
