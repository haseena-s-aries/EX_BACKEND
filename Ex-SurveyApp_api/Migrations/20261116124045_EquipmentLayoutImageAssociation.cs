using Ex_SurveyApp_api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations;

[DbContext(typeof(SurveyDbContext))]
[Migration("20261116124045_EquipmentLayoutImageAssociation")]
public partial class EquipmentLayoutImageAssociation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(name: "LayoutImageKey", table: "AreaEquipment", type: "nvarchar(80)", maxLength: 80, nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "LayoutImageKey", table: "AreaEquipment");
}
