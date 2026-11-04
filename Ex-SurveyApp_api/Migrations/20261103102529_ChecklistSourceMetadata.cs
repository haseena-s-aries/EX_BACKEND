using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class ChecklistSourceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChecklistQuestions_ChecklistTemplateId",
                table: "ChecklistQuestions");

            migrationBuilder.AddColumn<string>(
                name: "ApplicableGrades",
                table: "ChecklistQuestions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceItemNumber",
                table: "ChecklistQuestions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceItemReference",
                table: "ChecklistQuestions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubChecksJson",
                table: "ChecklistQuestions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistQuestions_ChecklistTemplateId_SourceItemReference",
                table: "ChecklistQuestions",
                columns: new[] { "ChecklistTemplateId", "SourceItemReference" },
                unique: true,
                filter: "[SourceItemReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChecklistQuestions_ChecklistTemplateId_SourceItemReference",
                table: "ChecklistQuestions");

            migrationBuilder.DropColumn(
                name: "ApplicableGrades",
                table: "ChecklistQuestions");

            migrationBuilder.DropColumn(
                name: "SourceItemNumber",
                table: "ChecklistQuestions");

            migrationBuilder.DropColumn(
                name: "SourceItemReference",
                table: "ChecklistQuestions");

            migrationBuilder.DropColumn(
                name: "SubChecksJson",
                table: "ChecklistQuestions");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistQuestions_ChecklistTemplateId",
                table: "ChecklistQuestions",
                column: "ChecklistTemplateId");
        }
    }
}
