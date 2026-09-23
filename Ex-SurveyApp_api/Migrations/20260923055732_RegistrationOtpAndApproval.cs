using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationOtpAndApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovalReviewedAt",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalReviewedByUserId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE [Users] SET [ApprovalStatus] = 1;");

            migrationBuilder.CreateTable(
                name: "RegistrationOtps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Otp = table.Column<string>(type: "nchar(4)", fixedLength: true, maxLength: 4, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationOtps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ApprovalReviewedByUserId",
                table: "Users",
                column: "ApprovalReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationOtps_Email",
                table: "RegistrationOtps",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ApprovalReviewedByUserId",
                table: "Users",
                column: "ApprovalReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ApprovalReviewedByUserId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "RegistrationOtps");

            migrationBuilder.DropIndex(
                name: "IX_Users_ApprovalReviewedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovalReviewedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovalReviewedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Users");
        }
    }
}
