using Ex_SurveyApp_api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations;

[DbContext(typeof(SurveyDbContext))]
[Migration("20261027090000_AddEquipmentRfidTagUid")]
public partial class AddEquipmentRfidTagUid : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RfidTagUid",
            table: "AreaEquipment",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AreaEquipment_RfidTagUid",
            table: "AreaEquipment",
            column: "RfidTagUid",
            unique: true,
            filter: "[RfidTagUid] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AreaEquipment_RfidTagUid",
            table: "AreaEquipment");

        migrationBuilder.DropColumn(
            name: "RfidTagUid",
            table: "AreaEquipment");
    }
}
