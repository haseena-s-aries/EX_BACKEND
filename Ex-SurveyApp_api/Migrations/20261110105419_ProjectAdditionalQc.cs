using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class ProjectAdditionalQc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdditionalQcId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AdditionalQcId",
                table: "Projects",
                column: "AdditionalQcId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_AdditionalQcId",
                table: "Projects",
                column: "AdditionalQcId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_AdditionalQcId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_AdditionalQcId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AdditionalQcId",
                table: "Projects");
        }
    }
}
