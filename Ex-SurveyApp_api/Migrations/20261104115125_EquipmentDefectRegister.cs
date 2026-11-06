using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentDefectRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SurveySessionId",
                table: "Findings",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "AdditionalRemarks",
                table: "Findings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AosdId",
                table: "Findings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaultCategory",
                table: "Findings",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaultCode",
                table: "Findings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegisterAuthorId",
                table: "Findings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RegisterDate",
                table: "Findings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegisterEquipmentId",
                table: "Findings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegisterGrade",
                table: "Findings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Findings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Findings_RegisterAuthorId",
                table: "Findings",
                column: "RegisterAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_RegisterEquipmentId",
                table: "Findings",
                column: "RegisterEquipmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Findings_AreaEquipment_RegisterEquipmentId",
                table: "Findings",
                column: "RegisterEquipmentId",
                principalTable: "AreaEquipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Findings_Users_RegisterAuthorId",
                table: "Findings",
                column: "RegisterAuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Findings] WHERE [SurveySessionId] IS NULL)
                    THROW 51000, 'Cannot revert EquipmentDefectRegister while equipment-only defects exist.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Findings_AreaEquipment_RegisterEquipmentId",
                table: "Findings");

            migrationBuilder.DropForeignKey(
                name: "FK_Findings_Users_RegisterAuthorId",
                table: "Findings");

            migrationBuilder.DropIndex(
                name: "IX_Findings_RegisterAuthorId",
                table: "Findings");

            migrationBuilder.DropIndex(
                name: "IX_Findings_RegisterEquipmentId",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "AdditionalRemarks",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "AosdId",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "FaultCategory",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "FaultCode",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "RegisterAuthorId",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "RegisterDate",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "RegisterEquipmentId",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "RegisterGrade",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Findings");

            migrationBuilder.AlterColumn<Guid>(
                name: "SurveySessionId",
                table: "Findings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
