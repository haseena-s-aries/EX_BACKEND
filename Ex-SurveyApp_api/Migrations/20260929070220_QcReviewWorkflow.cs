using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class QcReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QcReviews_Users_ReviewerId",
                table: "QcReviews");

            migrationBuilder.DropIndex(
                name: "IX_QcReviews_SurveySessionId",
                table: "QcReviews");

            migrationBuilder.RenameColumn(
                name: "ReviewerId",
                table: "QcReviews",
                newName: "ReviewedByUserId");

            migrationBuilder.RenameColumn(
                name: "Comment",
                table: "QcReviews",
                newName: "Comments");

            migrationBuilder.RenameIndex(
                name: "IX_QcReviews_ReviewerId",
                table: "QcReviews",
                newName: "IX_QcReviews_ReviewedByUserId");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                table: "SurveySessions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "EquipmentId",
                table: "SurveySessions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "AreaEquipmentId",
                table: "SurveySessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueDate",
                table: "SurveySessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InspectionTimestamp",
                table: "SurveySessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SurveyorRemarks",
                table: "SurveySessions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "QcReviews",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Comments",
                table: "QcReviews",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Severity",
                table: "QcReviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "QcReviews",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "QcReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE [QcReviews] SET [Status] = CASE WHEN [Approved] = 1 THEN 3 ELSE 2 END, [StartedAt] = [ReviewedAt]");

            migrationBuilder.DropColumn(
                name: "Approved",
                table: "QcReviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                table: "Files",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "SurveySessionId",
                table: "Files",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveySessions_AreaEquipmentId",
                table: "SurveySessions",
                column: "AreaEquipmentId",
                unique: true,
                filter: "[AreaEquipmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QcReviews_SurveySessionId",
                table: "QcReviews",
                column: "SurveySessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_SurveySessionId",
                table: "Files",
                column: "SurveySessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_SurveySessions_SurveySessionId",
                table: "Files",
                column: "SurveySessionId",
                principalTable: "SurveySessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QcReviews_Users_ReviewedByUserId",
                table: "QcReviews",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SurveySessions_AreaEquipment_AreaEquipmentId",
                table: "SurveySessions",
                column: "AreaEquipmentId",
                principalTable: "AreaEquipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_SurveySessions_SurveySessionId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_QcReviews_Users_ReviewedByUserId",
                table: "QcReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_SurveySessions_AreaEquipment_AreaEquipmentId",
                table: "SurveySessions");

            migrationBuilder.DropIndex(
                name: "IX_SurveySessions_AreaEquipmentId",
                table: "SurveySessions");

            migrationBuilder.DropIndex(
                name: "IX_QcReviews_SurveySessionId",
                table: "QcReviews");

            migrationBuilder.DropIndex(
                name: "IX_Files_SurveySessionId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "AreaEquipmentId",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "InspectionTimestamp",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "SurveyorRemarks",
                table: "SurveySessions");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "QcReviews");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "QcReviews");

            migrationBuilder.DropColumn(
                name: "SurveySessionId",
                table: "Files");

            migrationBuilder.RenameColumn(
                name: "ReviewedByUserId",
                table: "QcReviews",
                newName: "ReviewerId");

            migrationBuilder.RenameIndex(
                name: "IX_QcReviews_ReviewedByUserId",
                table: "QcReviews",
                newName: "IX_QcReviews_ReviewerId");

            migrationBuilder.AlterColumn<string>(
                name: "Comments",
                table: "QcReviews",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "Comments",
                table: "QcReviews",
                newName: "Comment");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                table: "SurveySessions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EquipmentId",
                table: "SurveySessions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "QcReviews",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                table: "QcReviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE [QcReviews] SET [Approved] = CASE WHEN [Status] = 3 THEN 1 ELSE 0 END");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "QcReviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                table: "Files",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QcReviews_SurveySessionId",
                table: "QcReviews",
                column: "SurveySessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_QcReviews_Users_ReviewerId",
                table: "QcReviews",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
