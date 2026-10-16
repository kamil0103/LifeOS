using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StructuredCompanyResearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Culture",
                table: "CompanyNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "CompanyNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterviewPreparation",
                table: "CompanyNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Products",
                table: "CompanyNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnologyStack",
                table: "CompanyNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "CompanyNotes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Culture",
                table: "CompanyNotes");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "CompanyNotes");

            migrationBuilder.DropColumn(
                name: "InterviewPreparation",
                table: "CompanyNotes");

            migrationBuilder.DropColumn(
                name: "Products",
                table: "CompanyNotes");

            migrationBuilder.DropColumn(
                name: "TechnologyStack",
                table: "CompanyNotes");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "CompanyNotes");
        }
    }
}
