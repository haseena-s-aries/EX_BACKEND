using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class ProjectReportStatusWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectReportWorkflows",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Ongoing"),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    HoldReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReportWorkflows", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectReportWorkflows_GeneratedReportArtifacts_SubmittedArtifactId",
                        column: x => x.SubmittedArtifactId,
                        principalTable: "GeneratedReportArtifacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectReportWorkflows_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportWorkflowEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportWorkflowEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportWorkflowEvents_GeneratedReportArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "GeneratedReportArtifacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReportWorkflowEvents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReportWorkflows_SubmittedArtifactId",
                table: "ProjectReportWorkflows",
                column: "SubmittedArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportWorkflowEvents_ArtifactId",
                table: "ReportWorkflowEvents",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportWorkflowEvents_ProjectId_Version",
                table: "ReportWorkflowEvents",
                columns: new[] { "ProjectId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectReportWorkflows");

            migrationBuilder.DropTable(
                name: "ReportWorkflowEvents");
        }
    }
}
