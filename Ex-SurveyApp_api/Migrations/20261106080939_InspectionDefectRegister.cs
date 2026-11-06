using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class InspectionDefectRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AosdId",
                table: "ActivityFindings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailedGrades",
                table: "ActivityFindings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaultCategory",
                table: "ActivityFindings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpRemarks",
                table: "ActivityFindings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegisterRemarks",
                table: "ActivityFindings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AosdId",
                table: "ActivityFindings");

            migrationBuilder.DropColumn(
                name: "FailedGrades",
                table: "ActivityFindings");

            migrationBuilder.DropColumn(
                name: "FaultCategory",
                table: "ActivityFindings");

            migrationBuilder.DropColumn(
                name: "FollowUpRemarks",
                table: "ActivityFindings");

            migrationBuilder.DropColumn(
                name: "RegisterRemarks",
                table: "ActivityFindings");
        }
    }
}
