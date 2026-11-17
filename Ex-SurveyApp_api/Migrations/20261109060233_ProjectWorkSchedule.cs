using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class ProjectWorkSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "TargetDate",
                table: "Projects",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectScheduleEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PreviousTargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WorkStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedWorkEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DelayReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DelayDays = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectScheduleEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectScheduleEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDelayEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDelayEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDelayEmails_ProjectScheduleEntries_ScheduleEntryId",
                        column: x => x.ScheduleEntryId,
                        principalTable: "ProjectScheduleEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDelayEmails_ScheduleEntryId_RecipientId",
                table: "ProjectDelayEmails",
                columns: new[] { "ScheduleEntryId", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDelayEmails_Status",
                table: "ProjectDelayEmails",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectScheduleEntries_ProjectId_EventKey",
                table: "ProjectScheduleEntries",
                columns: new[] { "ProjectId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectScheduleEntries_ProjectId_Kind_OccurredAt",
                table: "ProjectScheduleEntries",
                columns: new[] { "ProjectId", "Kind", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectDelayEmails");

            migrationBuilder.DropTable(
                name: "ProjectScheduleEntries");

            migrationBuilder.DropColumn(
                name: "TargetDate",
                table: "Projects");
        }
    }
}
