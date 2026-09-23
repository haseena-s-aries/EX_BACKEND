using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class RequestedRegistrationRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequestedRole",
                table: "RegistrationOtps",
                type: "int",
                nullable: false,
                defaultValue: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedRole",
                table: "RegistrationOtps");
        }
    }
}
