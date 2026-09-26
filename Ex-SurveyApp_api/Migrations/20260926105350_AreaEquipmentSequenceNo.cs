using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class AreaEquipmentSequenceNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SequenceNo",
                table: "AreaEquipment",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH NumberedEquipment AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY AreaId
                               ORDER BY CreatedAt, Id) AS NewSequenceNo
                    FROM AreaEquipment
                )
                UPDATE equipment
                SET SequenceNo = numbered.NewSequenceNo
                FROM AreaEquipment AS equipment
                INNER JOIN NumberedEquipment AS numbered ON numbered.Id = equipment.Id;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "SequenceNo",
                table: "AreaEquipment",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AreaEquipment_AreaId_SequenceNo",
                table: "AreaEquipment",
                columns: new[] { "AreaId", "SequenceNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AreaEquipment_AreaId_SequenceNo",
                table: "AreaEquipment");

            migrationBuilder.DropColumn(
                name: "SequenceNo",
                table: "AreaEquipment");
        }
    }
}
