using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class ProjectMultipleClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectClientAssignments",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectClientAssignments", x => new { x.ProjectId, x.ClientUserId });
                    table.ForeignKey(
                        name: "FK_ProjectClientAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectClientAssignments_Users_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClientAssignments_ClientUserId",
                table: "ProjectClientAssignments",
                column: "ClientUserId");
            // Retain the legacy column and copy every existing assignment without changing project metadata.
            migrationBuilder.Sql(@"INSERT INTO [ProjectClientAssignments] ([ProjectId], [ClientUserId], [IsActive], [AssignedAt], [UpdatedAt])
                SELECT p.[Id], p.[AssignedClientId], CAST(1 AS bit), p.[CreatedAt], p.[UpdatedAt]
                FROM [Projects] p
                WHERE p.[AssignedClientId] IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM [ProjectClientAssignments] a WHERE a.[ProjectId] = p.[Id] AND a.[ClientUserId] = p.[AssignedClientId]);");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectClientAssignments");
        }
    }
}
