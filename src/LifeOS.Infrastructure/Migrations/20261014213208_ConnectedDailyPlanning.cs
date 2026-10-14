using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConnectedDailyPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SnoozedUntil",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterEmail",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterName",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterPhone",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionKey",
                table: "CalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "CalendarEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReminderPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowUpsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CalendarEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CodingReviewsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CalendarLeadMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ReminderPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_SourceKey",
                table: "Notifications",
                columns: new[] { "UserId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_ApplicationId",
                table: "CalendarEvents",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_UserId_ActionKey",
                table: "CalendarEvents",
                columns: new[] { "UserId", "ActionKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_JobApplications_ApplicationId",
                table: "CalendarEvents",
                column: "ApplicationId",
                principalTable: "JobApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_JobApplications_ApplicationId",
                table: "CalendarEvents");

            migrationBuilder.DropTable(
                name: "ReminderPreferences");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_SourceKey",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_ApplicationId",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_UserId_ActionKey",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SnoozedUntil",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RecruiterEmail",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "RecruiterName",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "RecruiterPhone",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ActionKey",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "CalendarEvents");
        }
    }
}
