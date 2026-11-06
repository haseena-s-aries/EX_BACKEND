using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class InspectionGradeResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GradeResultsJson",
                table: "CheckpointResponseRevisions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionComment",
                table: "CheckpointResponseRevisions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GradeResultsJson",
                table: "CheckpointResponseRevisions");

            migrationBuilder.DropColumn(
                name: "InspectionComment",
                table: "CheckpointResponseRevisions");
        }
    }
}
