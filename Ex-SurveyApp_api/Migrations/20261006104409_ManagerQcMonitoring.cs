using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class ManagerQcMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "AuditLogs",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QcStatus",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewStartedAt",
                table: "AuditLogs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "AuditLogs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Severity",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerId",
                table: "Users",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityId_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "EntityId", "OccurredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Earlier review rounds were not stored. Preserve the available current snapshot
            // without inventing historic events or replacing existing comments.
            migrationBuilder.Sql("""
                INSERT INTO AuditLogs (Id, ActorId, EntityType, EntityId, Action, OccurredAt,
                    Comments, Severity, QcStatus, ReviewStartedAt, ReviewedAt)
                SELECT NEWID(), ReviewedByUserId, 'QcWorkflow', SurveySessionId,
                    'Existing QC Snapshot', COALESCE(ReviewedAt, StartedAt, SYSDATETIMEOFFSET()),
                    Comments, Severity, Status, StartedAt, ReviewedAt
                FROM QcReviews;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ManagerId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityId_OccurredAt",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "QcStatus",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ReviewStartedAt",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "AuditLogs");

        }
    }
}
