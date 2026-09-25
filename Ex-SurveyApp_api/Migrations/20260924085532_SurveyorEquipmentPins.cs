using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class SurveyorEquipmentPins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LayoutImagePath",
                table: "ProjectAreas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE pa
                SET LayoutImagePath = (
                    SELECT TOP (1) pap.FilePath
                    FROM ProjectAreaPhotos AS pap
                    WHERE pap.ProjectAreaId = pa.Id
                    ORDER BY pap.UploadedAt, pap.Id)
                FROM ProjectAreas AS pa
                WHERE pa.LayoutImagePath IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "AreaEquipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    XPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    YPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TagNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EquipmentCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CircuitId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProtectionDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CertificateNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AreaEquipment_ProjectAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "ProjectAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AreaEquipment_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AreaEquipment_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AreaEquipment_AreaId_TagNumber",
                table: "AreaEquipment",
                columns: new[] { "AreaId", "TagNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AreaEquipment_CreatedByUserId",
                table: "AreaEquipment",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AreaEquipment_ProjectId",
                table: "AreaEquipment",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AreaEquipment");

            migrationBuilder.DropColumn(
                name: "LayoutImagePath",
                table: "ProjectAreas");
        }
    }
}
