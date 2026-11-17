using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class AreaEquipmentMarkingAndReviewNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "MarkingNo", table: "AreaEquipment", type: "int", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "AreaId", table: "UserAccessNotifications", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "EquipmentId", table: "UserAccessNotifications", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "InspectionActivityId", table: "UserAccessNotifications", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "ProjectId", table: "UserAccessNotifications", type: "uniqueidentifier", nullable: true);

            // Preserve the marker number users already see. Future inserts allocate independently per area.
            migrationBuilder.Sql("UPDATE [AreaEquipment] SET [MarkingNo] = [SequenceNo] WHERE [MarkingNo] IS NULL;");
            migrationBuilder.CreateIndex(name: "IX_AreaEquipment_AreaId_MarkingNo", table: "AreaEquipment", columns: new[] { "AreaId", "MarkingNo" }, unique: true, filter: "[MarkingNo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_AreaEquipment_AreaId_MarkingNo", table: "AreaEquipment");
            migrationBuilder.DropColumn(name: "MarkingNo", table: "AreaEquipment");
            migrationBuilder.DropColumn(name: "AreaId", table: "UserAccessNotifications");
            migrationBuilder.DropColumn(name: "EquipmentId", table: "UserAccessNotifications");
            migrationBuilder.DropColumn(name: "InspectionActivityId", table: "UserAccessNotifications");
            migrationBuilder.DropColumn(name: "ProjectId", table: "UserAccessNotifications");
        }
    }
}
