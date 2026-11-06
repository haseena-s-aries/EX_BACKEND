using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class CategoryInspectionChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber_ProtectionTypeSnapshot",
                table: "ChecklistVersionSnapshots");

            migrationBuilder.AlterColumn<int>(
                name: "Grade",
                table: "InspectionActivities",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceChecklistTemplateId",
                table: "ChecklistVersionSnapshots",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<int>(
                name: "InspectionType",
                table: "ChecklistVersionSnapshots",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber_ProtectionTypeSnapshot",
                table: "ChecklistVersionSnapshots",
                columns: new[] { "SourceChecklistTemplateId", "VersionNumber", "ProtectionTypeSnapshot" },
                unique: true,
                filter: "[SourceChecklistTemplateId] IS NOT NULL AND [ProtectionTypeSnapshot] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [InspectionActivities] WHERE [Grade] IS NULL)
                    OR EXISTS (SELECT 1 FROM [ChecklistVersionSnapshots] WHERE [InspectionType] IS NULL OR [SourceChecklistTemplateId] IS NULL)
                    THROW 51000, 'Cannot revert while grade-free checklist records exist.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber_ProtectionTypeSnapshot",
                table: "ChecklistVersionSnapshots");

            migrationBuilder.AlterColumn<int>(
                name: "Grade",
                table: "InspectionActivities",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceChecklistTemplateId",
                table: "ChecklistVersionSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InspectionType",
                table: "ChecklistVersionSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber_ProtectionTypeSnapshot",
                table: "ChecklistVersionSnapshots",
                columns: new[] { "SourceChecklistTemplateId", "VersionNumber", "ProtectionTypeSnapshot" },
                unique: true,
                filter: "[ProtectionTypeSnapshot] IS NOT NULL");
        }
    }
}
