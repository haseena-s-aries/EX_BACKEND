using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class ProjectSurveyorHandover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdditionalSurveyorId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedByUserId",
                table: "InspectionActivities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedByUserId",
                table: "SurveySessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AdditionalSurveyorId",
                table: "Projects",
                column: "AdditionalSurveyorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_AdditionalSurveyorId",
                table: "Projects",
                column: "AdditionalSurveyorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_AdditionalSurveyorId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_AdditionalSurveyorId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AdditionalSurveyorId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LastModifiedByUserId",
                table: "InspectionActivities");

            migrationBuilder.DropColumn(
                name: "LastModifiedByUserId",
                table: "SurveySessions");
        }
    }
}
