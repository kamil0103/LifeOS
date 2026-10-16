using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SkillEvidenceAndAccomplishments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccomplishmentsJson",
                table: "WorkExperiences",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInResume",
                table: "WorkExperiences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "LearningStatus",
                table: "Skills",
                type: "text",
                nullable: false,
                defaultValue: "unspecified");

            migrationBuilder.AddColumn<bool>(
                name: "CertificateExpiryEnabled",
                table: "ReminderPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "AccomplishmentsJson",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInResume",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "SkillEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillEvidence_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillEvidence_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkillEvidence_SkillId_SourceType_SourceId",
                table: "SkillEvidence",
                columns: new[] { "SkillId", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillEvidence_UserId",
                table: "SkillEvidence",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkillEvidence");

            migrationBuilder.DropColumn(
                name: "AccomplishmentsJson",
                table: "WorkExperiences");

            migrationBuilder.DropColumn(
                name: "IncludeInResume",
                table: "WorkExperiences");

            migrationBuilder.DropColumn(
                name: "LearningStatus",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "CertificateExpiryEnabled",
                table: "ReminderPreferences");

            migrationBuilder.DropColumn(
                name: "AccomplishmentsJson",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IncludeInResume",
                table: "Projects");
        }
    }
}
