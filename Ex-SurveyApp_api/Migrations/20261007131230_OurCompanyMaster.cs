using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class OurCompanyMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OurCompanyId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyMasters", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CompanyMasters",
                columns: new[] { "Id", "Address", "CompanyName", "CreatedAt", "DisplayName", "Email", "Fax", "IsActive", "LogoPath", "Telephone", "UpdatedAt", "Website" },
                values: new object[,]
                {
                    { new Guid("235b2bcf-183f-5be0-86ab-0cdb29195920"), "P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE", "ARIES MARINE & ENGG. SERVICES", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Marine & ENGG .SERVICES - DF", "design@ariesgroup.ae, offshoredesign@ariesgroup.ae", "+971 (6) 550 3100", true, "aries_mar_engg.png", "+971 (6) 550 3300", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("31bdf1cc-fb23-547f-9b85-fbd048a24705"), "PO. Box: 15368, 3rd Floor, 303-B, Jaidah Square Umm Ghuwailina, 63 Airport Road - Zone 27, Doha, Qatar", "ARIES MARINE CO. W.L.L", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Marine CO. WLL - QD", "designqa@ariesgroup.ae", null, true, "aries_mar_engg.png", null, new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("50875e90-8c8e-57c5-8903-3810fb65ffba"), "P.O Box: 3965, Warehouse 40 Mussafah Ind.Area, Abu Dhabi, UAE", "ARIES OIL FIELD SERVICES L.L.C", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Oil Field Services L L .C - DU", "designauh@ariesofs.ae", "+971 (2) 550 8996", true, "aries_mar_engg.png", "+971 (2) 550 8893", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("54601c1b-c9fd-5199-a72e-a169a05970ae"), "P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE", "ARIES MARINE & ENGG. SERVICES", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Marine & ENGG .SERVICES - DI", "ideas@ariesofs.ae", "+971 (6) 550 3100", true, "aries_mar_engg.png", "+971 (6) 550 3300", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("651862ed-98e7-5a95-9c6f-dce2dbacd941"), "P.O Box: 94354, AI Qayadah Bldg. Abu Hail, Dubai, UAE", "ARIES MARINE L.L.C", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Marine L L .C - DS", "ariesmarine@ariesgroup.ae", "+971 (4) 299 7940", true, "aries_mar_engg.png", "+971 (4) 299 7939", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("9003d8ae-58d2-577d-a9b3-d145e9bb738f"), "P.O Box: 94354, Al Qayadah Bldg. Abu Hail, Dubai, UAE", "ARIES MARINE L.L.C", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Marine L L .C - DM", "dams@ariesgroup.ae", "+971 (4) 299 7940", true, "aries_mar_engg.png", "+971 (4) 299 7939", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("986c73ab-be16-508b-a484-e2260d267ee5"), "Business Bay, Office #601, Buiding No. 1260, Road No.  2421, Block No.  324, P.O. Box. 11614, Juffair, Manama, Kingdom of Bahrain", "ARIES MARINE WLL", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Marine WLL - DB", "ariesbahrain@ariesgroup.ae", null, true, "aries_mar_engg.png", null, new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("9caaffe9-ed24-587e-91ea-764cb5876674"), "#22-03A, International Plaza 10 Anson Road, Singapore 079903", "ARIES NETWORK PTE. LTD.", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Network PTE .LTD - DG", "designsg@ariesgroup.ae", "+65 6220 9058", true, "aries_mar_engg.png", "+65 6220 9056", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("cc0f18d3-8516-5c7b-99ad-54148151b25f"), "P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE", "ARIES MARINE & ENGG. SERVICES", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Marine & ENGG .SERVICES - DE", "eit@ariesgroup.ae", "+971 (6) 550 3100", true, "aries_mar_engg.png", "+971 (6) 550 3300", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("d31b47a1-91a5-5acf-b7bf-98610b0fcc2a"), "P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE", "ARIES MARINE & ENGG. SERVICES", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Marine & ENGG .SERVICES - DN", "design@ariesgroup.ae", "+971 (6) 550 3100", true, "aries_mar_engg.png", "+971 (6) 550 3300", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" },
                    { new Guid("fb2c0295-cc98-5af3-90e8-c78d8aee925d"), "Juffali Building (Fluor Arabia Tower) Office #504, King Fahd Road, Al Khobar 34423 Kingdom of Saudi Arabia", "ARIES MARINE SERVICES CO. LTD.", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aries Marine Services CO .LTD - KD", "ariesksa@ariesgroup.ae", null, true, "aries_mar_engg.png", null, new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "www.ariesmar.com" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OurCompanyId",
                table: "Projects",
                column: "OurCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMasters_DisplayName",
                table: "CompanyMasters",
                column: "DisplayName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_CompanyMasters_OurCompanyId",
                table: "Projects",
                column: "OurCompanyId",
                principalTable: "CompanyMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_CompanyMasters_OurCompanyId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "CompanyMasters");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OurCompanyId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OurCompanyId",
                table: "Projects");
        }
    }
}
