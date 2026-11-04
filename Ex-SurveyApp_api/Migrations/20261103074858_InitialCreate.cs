using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ex_SurveyApp_api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogoFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "RegistrationOtps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedRole = table.Column<int>(type: "int", nullable: false),
                    Otp = table.Column<string>(type: "nchar(4)", fixedLength: true, maxLength: 4, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerificationAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConcurrencyVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationOtps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Provisioning"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    ApprovalReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Users_ApprovalReviewedByUserId",
                        column: x => x.ApprovalReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Uid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegacyEntityMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceRecordKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, defaultValue: "Proposed"),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyEntityMaps", x => x.Id);
                    table.CheckConstraint("CK_LegacyEntityMaps_VerifiedTarget", "[Status] <> N'Verified' OR ([TargetEntityType] IS NOT NULL AND [TargetId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LegacyEntityMaps_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantBackfillExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MigrationBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceRecordKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExceptionCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, defaultValue: "Open"),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBackfillExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBackfillExceptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StatusCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantContracts", x => x.Id);
                    table.UniqueConstraint("AK_TenantContracts_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_TenantContracts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: true),
                    QcStatus = table.Column<int>(type: "int", nullable: true),
                    ReviewStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InspectionType = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    DraftRevision = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LifecycleStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    StandardReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StandardEdition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ControlledSourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApplicableProtectionTypes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistTemplates_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChecklistTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChecklistTemplates_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedReportArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReportNumber = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedReportArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedReportArtifacts_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    JobNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    VesselOrSiteName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HazardousAreaPlanPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HazardousAreaPlanFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UidNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientLogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OurCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedSurveyorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedQcId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_CompanyMasters_OurCompanyId",
                        column: x => x.OurCompanyId,
                        principalTable: "CompanyMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_Users_AssignedClientId",
                        column: x => x.AssignedClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_Users_AssignedQcId",
                        column: x => x.AssignedQcId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_Users_AssignedSurveyorId",
                        column: x => x.AssignedSurveyorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SyncBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientBatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncBatches_Users_SurveyorId",
                        column: x => x.SurveyorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Invited"),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMemberships", x => x.Id);
                    table.UniqueConstraint("AK_TenantMemberships_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_TenantMemberships_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantMemberships_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Areas_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobAssignments_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    NumericReadingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MeasurementUnit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MinimumReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MaximumReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    NumericReadingRequired = table.Column<bool>(type: "bit", nullable: false),
                    SectionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApplicableProtectionTypes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequireRemarkOnFail = table.Column<bool>(type: "bit", nullable: false),
                    ResponseScaleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistQuestions_ChecklistTemplates_ChecklistTemplateId",
                        column: x => x.ChecklistTemplateId,
                        principalTable: "ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistVersionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceChecklistTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    InspectionType = table.Column<int>(type: "int", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StandardReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StandardEdition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ControlledSourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProtectionTypeSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistVersionSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistVersionSnapshots_ChecklistTemplates_SourceChecklistTemplateId",
                        column: x => x.SourceChecklistTemplateId,
                        principalTable: "ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChecklistVersionSnapshots_Users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionLeadUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClosePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DetailedPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    MinimumDetailedCreditPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    RoundingRule = table.Column<int>(type: "int", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    AllocationApproved = table.Column<bool>(type: "bit", nullable: false),
                    AllocationApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllocationApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionCampaigns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionCampaigns_Users_InspectionLeadUserId",
                        column: x => x.InspectionLeadUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionPlans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LayoutImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAreas_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportClientRemarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgainstReport = table.Column<bool>(type: "bit", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportClientRemarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportClientRemarks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReportClientRemarks_Users_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantProjectBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantProjectBindings", x => x.Id);
                    table.UniqueConstraint("AK_TenantProjectBindings_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_TenantProjectBindings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantProjectBindings_TenantContracts_TenantId_ContractId",
                        columns: x => new { x.TenantId, x.ContractId },
                        principalTable: "TenantContracts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantRoleGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRoleGrants", x => x.Id);
                    table.CheckConstraint("CK_TenantRoleGrants_ScopeShape", "([ScopeType] = N'Tenant' AND [ScopeId] IS NULL) OR ([ScopeType] <> N'Tenant' AND [ScopeId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TenantRoleGrants_TenantMemberships_TenantId_TenantMembershipId",
                        columns: x => new { x.TenantId, x.TenantMembershipId },
                        principalTable: "TenantMemberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExMarking = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RfidTag = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipment_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CheckpointDefinitionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistVersionSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QuestionVersion = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ApplicabilityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponseScaleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    NumericReadingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MeasurementUnit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MinimumReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MaximumReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    NumericReadingRequired = table.Column<bool>(type: "bit", nullable: false),
                    SectionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RequireRemarkOnFail = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckpointDefinitionSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckpointDefinitionSnapshots_ChecklistVersionSnapshots_ChecklistVersionSnapshotId",
                        column: x => x.ChecklistVersionSnapshotId,
                        principalTable: "ChecklistVersionSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignAnnualLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    EquipmentCount = table.Column<int>(type: "int", nullable: false),
                    PlannedCloseCount = table.Column<int>(type: "int", nullable: true),
                    PlannedDetailedCount = table.Column<int>(type: "int", nullable: true),
                    ActualCloseCount = table.Column<int>(type: "int", nullable: true),
                    ActualDetailedCount = table.Column<int>(type: "int", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAnnualLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAnnualLists_InspectionCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "InspectionCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionWorkPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Area = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rotation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PlannedStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionWorkPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionWorkPackages_InspectionCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "InspectionCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionWorkPackages_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanningApprovalEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningApprovalEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningApprovalEvents_InspectionCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "InspectionCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AreaEquipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    XPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    YPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsPlacedOnLayout = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TagNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EquipmentCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RfidTagUid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CircuitId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProtectionDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CertificateNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ExProtectionType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    GasGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TemperatureClass = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HazardousAreaZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AmbientTemperatureRange = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpRating = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NotifiedBody = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SpecialConditionsOfUse = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RfidIdentificationMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NotApplicableFieldsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReconciliationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Unreviewed"),
                    RegistrationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Verified"),
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

            migrationBuilder.CreateTable(
                name: "ProjectAreaPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectAreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAreaPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAreaPhotos_ProjectAreas_ProjectAreaId",
                        column: x => x.ProjectAreaId,
                        principalTable: "ProjectAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectAreaPhotos_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantProjectBindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SiteType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Provisioning"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSites", x => x.Id);
                    table.UniqueConstraint("AK_TenantSites_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_TenantSites_TenantProjectBindings_TenantId_TenantProjectBindingId",
                        columns: x => new { x.TenantId, x.TenantProjectBindingId },
                        principalTable: "TenantProjectBindings",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantSites_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionRecurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnnualListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Grade = table.Column<int>(type: "int", nullable: false),
                    EveryMonths = table.Column<int>(type: "int", nullable: false),
                    NextOccurrenceOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    LastGeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionRecurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionRecurrences_CampaignAnnualLists_AnnualListId",
                        column: x => x.AnnualListId,
                        principalTable: "CampaignAnnualLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionRecurrences_InspectionCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "InspectionCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SamplingApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignRevision = table.Column<int>(type: "int", nullable: false),
                    AnnualListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    RuleVersion = table.Column<int>(type: "int", nullable: false),
                    RandomSeed = table.Column<int>(type: "int", nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SelectedEquipmentIdsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 2000000, nullable: false),
                    OverridesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 2000000, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsFrozen = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplingApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SamplingApprovals_CampaignAnnualLists_AnnualListId",
                        column: x => x.AnnualListId,
                        principalTable: "CampaignAnnualLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SamplingApprovals_InspectionCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "InspectionCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentDocumentRequirementPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EquipmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentDocumentRequirementPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentDocumentRequirementPolicies_InspectionWorkPackages_WorkPackageId",
                        column: x => x.WorkPackageId,
                        principalTable: "InspectionWorkPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentDocumentRequirementPolicies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignEquipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnnualListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaEquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskScore = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    EquipmentTypeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignEquipment_AreaEquipment_AreaEquipmentId",
                        column: x => x.AreaEquipmentId,
                        principalTable: "AreaEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignEquipment_CampaignAnnualLists_AnnualListId",
                        column: x => x.AnnualListId,
                        principalTable: "CampaignAnnualLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignEquipment_InspectionCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "InspectionCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentReferenceDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersedesDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Revision = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IssuingOrganization = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentReferenceDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentReferenceDocuments_AreaEquipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "AreaEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentReferenceDocuments_EquipmentReferenceDocuments_SupersedesDocumentId",
                        column: x => x.SupersedesDocumentId,
                        principalTable: "EquipmentReferenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentReferenceDocuments_ProjectAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "ProjectAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentReferenceDocuments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentReferenceDocuments_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentReferenceDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentScanEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Technology = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MatchResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Discrepancy = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ScannedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentScanEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentScanEvents_AreaEquipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "AreaEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentScanEvents_ProjectAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "ProjectAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentScanEvents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentScanEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentTagAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Technology = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NormalizedIdentifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssignmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DeactivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeactivatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeactivationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentTagAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentTagAssignments_AreaEquipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "AreaEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTagAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTagAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTagAssignments_Users_DeactivatedByUserId",
                        column: x => x.DeactivatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentTagHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Technology = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NormalizedIdentifier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActionCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentTagHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentTagHistory_AreaEquipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "AreaEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTagHistory_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTagHistory_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaEquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegacyEquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ScheduleReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkPackageReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AssignedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlannedGrade = table.Column<int>(type: "int", nullable: true),
                    RouteOrder = table.Column<int>(type: "int", nullable: true),
                    Rotation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriorityCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiredPermits = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsolationPrerequisites = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChecklistVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsInaccessible = table.Column<bool>(type: "bit", nullable: false),
                    CarryForwardReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StatusCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionTasks", x => x.Id);
                    table.CheckConstraint("CK_InspectionTasks_OneEquipment", "([AreaEquipmentId] IS NOT NULL AND [LegacyEquipmentId] IS NULL) OR ([AreaEquipmentId] IS NULL AND [LegacyEquipmentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InspectionTasks_AreaEquipment_AreaEquipmentId",
                        column: x => x.AreaEquipmentId,
                        principalTable: "AreaEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionTasks_ChecklistVersionSnapshots_ChecklistVersionId",
                        column: x => x.ChecklistVersionId,
                        principalTable: "ChecklistVersionSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionTasks_Equipment_LegacyEquipmentId",
                        column: x => x.LegacyEquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionTasks_InspectionPlans_InspectionPlanId",
                        column: x => x.InspectionPlanId,
                        principalTable: "InspectionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionTasks_InspectionWorkPackages_WorkPackageId",
                        column: x => x.WorkPackageId,
                        principalTable: "InspectionWorkPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionTasks_Users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurveySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AreaEquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SurveyorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InspectorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SurveyorRemarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InspectionTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveySessions_AreaEquipment_AreaEquipmentId",
                        column: x => x.AreaEquipmentId,
                        principalTable: "AreaEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurveySessions_ChecklistTemplates_ChecklistTemplateId",
                        column: x => x.ChecklistTemplateId,
                        principalTable: "ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurveySessions_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurveySessions_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurveySessions_Users_SurveyorId",
                        column: x => x.SurveyorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentReferenceDocumentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentReferenceDocumentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentReferenceDocumentEvents_EquipmentReferenceDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "EquipmentReferenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentReferenceDocumentEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionTaskAssignmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionTaskAssignmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionTaskAssignmentEvents_InspectionTasks_InspectionTaskId",
                        column: x => x.InspectionTaskId,
                        principalTable: "InspectionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Findings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Observation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_SurveySessions_SurveySessionId",
                        column: x => x.SurveySessionId,
                        principalTable: "SurveySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacySurveySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Grade = table.Column<int>(type: "int", nullable: false),
                    InspectorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectorNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ChecklistVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionActivities_ChecklistVersionSnapshots_ChecklistVersionId",
                        column: x => x.ChecklistVersionId,
                        principalTable: "ChecklistVersionSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionActivities_InspectionTasks_InspectionTaskId",
                        column: x => x.InspectionTaskId,
                        principalTable: "InspectionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionActivities_SurveySessions_LegacySurveySessionId",
                        column: x => x.LegacySurveySessionId,
                        principalTable: "SurveySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionActivities_Users_InspectorUserId",
                        column: x => x.InspectorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QcReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QcReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QcReviews_SurveySessions_SurveySessionId",
                        column: x => x.SurveySessionId,
                        principalTable: "SurveySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QcReviews_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SurveyAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyAnswers_ChecklistQuestions_ChecklistQuestionId",
                        column: x => x.ChecklistQuestionId,
                        principalTable: "ChecklistQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SurveyAnswers_SurveySessions_SurveySessionId",
                        column: x => x.SurveySessionId,
                        principalTable: "SurveySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SurveySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AreaEquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EquipmentPhotoRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Files_AreaEquipment_AreaEquipmentId",
                        column: x => x.AreaEquipmentId,
                        principalTable: "AreaEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Files_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Files_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Files_Findings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "Findings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Files_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Files_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Files_SurveySessions_SurveySessionId",
                        column: x => x.SurveySessionId,
                        principalTable: "SurveySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Files_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityReviewEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityReviewEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityReviewEvents_InspectionActivities_InspectionActivityId",
                        column: x => x.InspectionActivityId,
                        principalTable: "InspectionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityReviewEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentDocumentUseEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentRevision = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentDocumentUseEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentDocumentUseEvents_EquipmentReferenceDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "EquipmentReferenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentDocumentUseEvents_InspectionActivities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "InspectionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentDocumentUseEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionActivityDocumentUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentRevision = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionActivityDocumentUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionActivityDocumentUsages_InspectionActivities_InspectionActivityId",
                        column: x => x.InspectionActivityId,
                        principalTable: "InspectionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionActivityDocumentUsages_InspectionTasks_InspectionTaskId",
                        column: x => x.InspectionTaskId,
                        principalTable: "InspectionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionActivityDocumentUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionActivityRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionActivityRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionActivityRevisions_InspectionActivities_InspectionActivityId",
                        column: x => x.InspectionActivityId,
                        principalTable: "InspectionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionActivityRevisions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfflineSyncReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ServerActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseMapJson = table.Column<string>(type: "nvarchar(max)", maxLength: 200000, nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineSyncReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfflineSyncReceipts_InspectionActivities_ServerActivityId",
                        column: x => x.ServerActivityId,
                        principalTable: "InspectionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflineSyncReceipts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportRevisions_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportRevisions_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CheckpointResponseRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckpointDefinitionSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ApplicabilityResultCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponseCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TechnicalConditionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkflowState = table.Column<int>(type: "int", nullable: false),
                    Observation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NumericValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MeasurementUnitSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckpointResponseRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckpointResponseRevisions_CheckpointDefinitionSnapshots_CheckpointDefinitionSnapshotId",
                        column: x => x.CheckpointDefinitionSnapshotId,
                        principalTable: "CheckpointDefinitionSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckpointResponseRevisions_InspectionActivityRevisions_ActivityRevisionId",
                        column: x => x.ActivityRevisionId,
                        principalTable: "InspectionActivityRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckpointResponseRevisions_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckpointResponseRevisions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckpointResponseRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyFindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Observation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ProposedAction = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SeverityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FaultCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequiredMaterialsOrSupport = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')"),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StatusCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityFindings_CheckpointResponseRevisions_CheckpointResponseRevisionId",
                        column: x => x.CheckpointResponseRevisionId,
                        principalTable: "CheckpointResponseRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityFindings_Findings_LegacyFindingId",
                        column: x => x.LegacyFindingId,
                        principalTable: "Findings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityFindings_InspectionActivities_InspectionActivityId",
                        column: x => x.InspectionActivityId,
                        principalTable: "InspectionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityFindings_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityFindings_Users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckpointReviewEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckpointResponseRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckpointReviewEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckpointReviewEvents_CheckpointResponseRevisions_CheckpointResponseRevisionId",
                        column: x => x.CheckpointResponseRevisionId,
                        principalTable: "CheckpointResponseRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckpointReviewEvents_InspectionActivities_InspectionActivityId",
                        column: x => x.InspectionActivityId,
                        principalTable: "InspectionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckpointReviewEvents_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityFindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SupplementaryInformation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ProposedAction = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExternalWorkOrderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RectificationDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RectificationAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AriesVerifierUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AriesVerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClientDecisionUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientDecisionAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastDecisionComment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsOutstanding = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_ActivityFindings_ActivityFindingId",
                        column: x => x.ActivityFindingId,
                        principalTable: "ActivityFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_Users_AriesVerifierUserId",
                        column: x => x.AriesVerifierUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_Users_ClientDecisionUserId",
                        column: x => x.ClientDecisionUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_Users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveActionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectiveActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ResultingStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RectificationDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RectificationAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WorkOrderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionEvents_CorrectiveActions_CorrectiveActionId",
                        column: x => x.CorrectiveActionId,
                        principalTable: "CorrectiveActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AreaEquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegacyEquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckpointResponseRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectiveActionEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IntegrityHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityEvidence_AreaEquipment_AreaEquipmentId",
                        column: x => x.AreaEquipmentId,
                        principalTable: "AreaEquipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvidence_CheckpointResponseRevisions_CheckpointResponseRevisionId",
                        column: x => x.CheckpointResponseRevisionId,
                        principalTable: "CheckpointResponseRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvidence_CorrectiveActionEvents_CorrectiveActionEventId",
                        column: x => x.CorrectiveActionEventId,
                        principalTable: "CorrectiveActionEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvidence_Equipment_LegacyEquipmentId",
                        column: x => x.LegacyEquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvidence_Files_SurveyFileId",
                        column: x => x.SurveyFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvidence_InspectionActivities_InspectionActivityId",
                        column: x => x.InspectionActivityId,
                        principalTable: "InspectionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityEvidence_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfflinePhotoSyncReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckpointResponseRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflinePhotoSyncReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfflinePhotoSyncReceipts_ActivityEvidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "ActivityEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflinePhotoSyncReceipts_CheckpointResponseRevisions_CheckpointResponseRevisionId",
                        column: x => x.CheckpointResponseRevisionId,
                        principalTable: "CheckpointResponseRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflinePhotoSyncReceipts_InspectionActivities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "InspectionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflinePhotoSyncReceipts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IX_ActivityEvidence_AddedByUserId",
                table: "ActivityEvidence",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidence_AreaEquipmentId",
                table: "ActivityEvidence",
                column: "AreaEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidence_CheckpointResponseRevisionId",
                table: "ActivityEvidence",
                column: "CheckpointResponseRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidence_CorrectiveActionEventId",
                table: "ActivityEvidence",
                column: "CorrectiveActionEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidence_InspectionActivityId_AddedAt",
                table: "ActivityEvidence",
                columns: new[] { "InspectionActivityId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidence_LegacyEquipmentId",
                table: "ActivityEvidence",
                column: "LegacyEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvidence_SurveyFileId",
                table: "ActivityEvidence",
                column: "SurveyFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFindings_CheckpointResponseRevisionId",
                table: "ActivityFindings",
                column: "CheckpointResponseRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFindings_InspectionActivityId",
                table: "ActivityFindings",
                column: "InspectionActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFindings_LegacyFindingId",
                table: "ActivityFindings",
                column: "LegacyFindingId",
                unique: true,
                filter: "[LegacyFindingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFindings_ReportedByUserId",
                table: "ActivityFindings",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFindings_ResponsibleUserId",
                table: "ActivityFindings",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReviewEvents_ActorUserId",
                table: "ActivityReviewEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReviewEvents_InspectionActivityId_DecidedAt",
                table: "ActivityReviewEvents",
                columns: new[] { "InspectionActivityId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AreaEquipment_AreaId_SequenceNo",
                table: "AreaEquipment",
                columns: new[] { "AreaId", "SequenceNo" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_AreaEquipment_RfidTagUid",
                table: "AreaEquipment",
                column: "RfidTagUid",
                unique: true,
                filter: "[RfidTagUid] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_JobId",
                table: "Areas",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorId",
                table: "AuditLogs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityId_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAnnualLists_CampaignId_Year",
                table: "CampaignAnnualLists",
                columns: new[] { "CampaignId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignEquipment_AnnualListId",
                table: "CampaignEquipment",
                column: "AnnualListId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignEquipment_AreaEquipmentId",
                table: "CampaignEquipment",
                column: "AreaEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignEquipment_CampaignId_AreaEquipmentId",
                table: "CampaignEquipment",
                columns: new[] { "CampaignId", "AreaEquipmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistQuestions_ChecklistTemplateId",
                table: "ChecklistQuestions",
                column: "ChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_ApprovedByUserId",
                table: "ChecklistTemplates",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_CreatedByUserId",
                table: "ChecklistTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_FamilyId_Version",
                table: "ChecklistTemplates",
                columns: new[] { "FamilyId", "Version" },
                unique: true,
                filter: "[FamilyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_SubmittedByUserId",
                table: "ChecklistTemplates",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistVersionSnapshots_PublishedByUserId",
                table: "ChecklistVersionSnapshots",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber_ProtectionTypeSnapshot",
                table: "ChecklistVersionSnapshots",
                columns: new[] { "SourceChecklistTemplateId", "VersionNumber", "ProtectionTypeSnapshot" },
                unique: true,
                filter: "[ProtectionTypeSnapshot] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CheckpointDefinitionSnapshots_ChecklistVersionSnapshotId_QuestionCode_QuestionVersion",
                table: "CheckpointDefinitionSnapshots",
                columns: new[] { "ChecklistVersionSnapshotId", "QuestionCode", "QuestionVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckpointResponseRevisions_ActivityRevisionId_CheckpointDefinitionSnapshotId_RevisionNumber",
                table: "CheckpointResponseRevisions",
                columns: new[] { "ActivityRevisionId", "CheckpointDefinitionSnapshotId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckpointResponseRevisions_CheckpointDefinitionSnapshotId",
                table: "CheckpointResponseRevisions",
                column: "CheckpointDefinitionSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckpointResponseRevisions_RecordedByUserId",
                table: "CheckpointResponseRevisions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckpointResponseRevisions_ReviewedByUserId",
                table: "CheckpointResponseRevisions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckpointReviewEvents_CheckpointResponseRevisionId",
                table: "CheckpointReviewEvents",
                column: "CheckpointResponseRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckpointReviewEvents_InspectionActivityId_ReviewedAt",
                table: "CheckpointReviewEvents",
                columns: new[] { "InspectionActivityId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckpointReviewEvents_ReviewerUserId",
                table: "CheckpointReviewEvents",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMasters_DisplayName",
                table: "CompanyMasters",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionEvents_ActorUserId",
                table: "CorrectiveActionEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionEvents_CorrectiveActionId_OccurredAt",
                table: "CorrectiveActionEvents",
                columns: new[] { "CorrectiveActionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_ActionReference",
                table: "CorrectiveActions",
                column: "ActionReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_ActivityFindingId_CreatedAt",
                table: "CorrectiveActions",
                columns: new[] { "ActivityFindingId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_AriesVerifierUserId",
                table: "CorrectiveActions",
                column: "AriesVerifierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_ClientDecisionUserId",
                table: "CorrectiveActions",
                column: "ClientDecisionUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_ResponsibleUserId",
                table: "CorrectiveActions",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_AreaId_TagNumber",
                table: "Equipment",
                columns: new[] { "AreaId", "TagNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocumentRequirementPolicies_ProjectId_EquipmentType_WorkPackageId_CategoryCode_IsApproved",
                table: "EquipmentDocumentRequirementPolicies",
                columns: new[] { "ProjectId", "EquipmentType", "WorkPackageId", "CategoryCode", "IsApproved" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocumentRequirementPolicies_WorkPackageId",
                table: "EquipmentDocumentRequirementPolicies",
                column: "WorkPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocumentUseEvents_ActivityId_DocumentId_DocumentRevision_UserId_OpenedAt",
                table: "EquipmentDocumentUseEvents",
                columns: new[] { "ActivityId", "DocumentId", "DocumentRevision", "UserId", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocumentUseEvents_DocumentId",
                table: "EquipmentDocumentUseEvents",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocumentUseEvents_UserId",
                table: "EquipmentDocumentUseEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentReferenceDocumentEvents_ActorUserId",
                table: "EquipmentReferenceDocumentEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentReferenceDocumentEvents_DocumentId_OccurredAt",
                table: "EquipmentReferenceDocumentEvents",
                columns: new[] { "DocumentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentReferenceDocuments_ApprovedByUserId",
                table: "EquipmentReferenceDocuments",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentReferenceDocuments_AreaId",
                table: "EquipmentReferenceDocuments",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentReferenceDocuments_EquipmentId",
                table: "EquipmentReferenceDocuments",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentReferenceDocuments_ProjectId_EquipmentId_CategoryCode_Status",
                table: "EquipmentReferenceDocuments",
                columns: new[] { "ProjectId", "EquipmentId", "CategoryCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentReferenceDocuments_SupersedesDocumentId",
                table: "EquipmentReferenceDocuments",
                column: "SupersedesDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentReferenceDocuments_UploadedByUserId",
                table: "EquipmentReferenceDocuments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentScanEvents_ActorUserId",
                table: "EquipmentScanEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentScanEvents_AreaId",
                table: "EquipmentScanEvents",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentScanEvents_ClientEventId",
                table: "EquipmentScanEvents",
                column: "ClientEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentScanEvents_EquipmentId",
                table: "EquipmentScanEvents",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentScanEvents_ProjectId_AreaId_ScannedAt",
                table: "EquipmentScanEvents",
                columns: new[] { "ProjectId", "AreaId", "ScannedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTagAssignments_AssignedByUserId",
                table: "EquipmentTagAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTagAssignments_DeactivatedByUserId",
                table: "EquipmentTagAssignments",
                column: "DeactivatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTagAssignments_EquipmentId_Technology",
                table: "EquipmentTagAssignments",
                columns: new[] { "EquipmentId", "Technology" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTagAssignments_NormalizedIdentifier",
                table: "EquipmentTagAssignments",
                column: "NormalizedIdentifier",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTagAssignments_ProjectId",
                table: "EquipmentTagAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTagHistory_ActorUserId",
                table: "EquipmentTagHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTagHistory_AssignmentId",
                table: "EquipmentTagHistory",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTagHistory_EquipmentId_OccurredAt",
                table: "EquipmentTagHistory",
                columns: new[] { "EquipmentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTagHistory_ProjectId",
                table: "EquipmentTagHistory",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_AreaEquipmentId",
                table: "Files",
                column: "AreaEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_AreaId",
                table: "Files",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_EquipmentId",
                table: "Files",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_FindingId",
                table: "Files",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_JobId",
                table: "Files",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_ProjectId_AreaEquipmentId_UploadedAt",
                table: "Files",
                columns: new[] { "ProjectId", "AreaEquipmentId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Files_SurveySessionId",
                table: "Files",
                column: "SurveySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_UploadedById",
                table: "Files",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_SurveySessionId",
                table: "Findings",
                column: "SurveySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReportArtifacts_GeneratedByUserId",
                table: "GeneratedReportArtifacts",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReportArtifacts_ProjectId_GeneratedAt",
                table: "GeneratedReportArtifacts",
                columns: new[] { "ProjectId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReportArtifacts_ProjectId_ReportType_LinkedEntityId_Revision",
                table: "GeneratedReportArtifacts",
                columns: new[] { "ProjectId", "ReportType", "LinkedEntityId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionActivities_ChecklistVersionId",
                table: "InspectionActivities",
                column: "ChecklistVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionActivities_InspectionTaskId",
                table: "InspectionActivities",
                column: "InspectionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionActivities_InspectorUserId",
                table: "InspectionActivities",
                column: "InspectorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionActivities_LegacySurveySessionId",
                table: "InspectionActivities",
                column: "LegacySurveySessionId",
                unique: true,
                filter: "[LegacySurveySessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionActivityDocumentUsages_InspectionActivityId_DocumentIdentity_DocumentRevision",
                table: "InspectionActivityDocumentUsages",
                columns: new[] { "InspectionActivityId", "DocumentIdentity", "DocumentRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionActivityDocumentUsages_InspectionTaskId",
                table: "InspectionActivityDocumentUsages",
                column: "InspectionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionActivityDocumentUsages_UserId",
                table: "InspectionActivityDocumentUsages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionActivityRevisions_CreatedByUserId",
                table: "InspectionActivityRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionActivityRevisions_InspectionActivityId_RevisionNumber",
                table: "InspectionActivityRevisions",
                columns: new[] { "InspectionActivityId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionCampaigns_InspectionLeadUserId",
                table: "InspectionCampaigns",
                column: "InspectionLeadUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionCampaigns_ProjectId_Reference",
                table: "InspectionCampaigns",
                columns: new[] { "ProjectId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPlans_ProjectId",
                table: "InspectionPlans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecurrences_AnnualListId",
                table: "InspectionRecurrences",
                column: "AnnualListId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecurrences_CampaignId_AnnualListId_Grade_NextOccurrenceOn",
                table: "InspectionRecurrences",
                columns: new[] { "CampaignId", "AnnualListId", "Grade", "NextOccurrenceOn" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTaskAssignmentEvents_InspectionTaskId_ChangedAt",
                table: "InspectionTaskAssignmentEvents",
                columns: new[] { "InspectionTaskId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_AreaEquipmentId",
                table: "InspectionTasks",
                column: "AreaEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_AssignedUserId",
                table: "InspectionTasks",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_ChecklistVersionId",
                table: "InspectionTasks",
                column: "ChecklistVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_InspectionPlanId_ScheduledAt",
                table: "InspectionTasks",
                columns: new[] { "InspectionPlanId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_LegacyEquipmentId",
                table: "InspectionTasks",
                column: "LegacyEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTasks_WorkPackageId_RouteOrder",
                table: "InspectionTasks",
                columns: new[] { "WorkPackageId", "RouteOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionWorkPackages_CampaignId_Reference",
                table: "InspectionWorkPackages",
                columns: new[] { "CampaignId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionWorkPackages_ProjectId",
                table: "InspectionWorkPackages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_JobAssignments_JobId_UserId_Role",
                table: "JobAssignments",
                columns: new[] { "JobId", "UserId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobAssignments_UserId",
                table: "JobAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ClientId",
                table: "Jobs",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_JobNumber",
                table: "Jobs",
                column: "JobNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyEntityMaps_SourceSystem_SourceEntityType_SourceRecordKey",
                table: "LegacyEntityMaps",
                columns: new[] { "SourceSystem", "SourceEntityType", "SourceRecordKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyEntityMaps_TenantId_TargetEntityType_TargetId",
                table: "LegacyEntityMaps",
                columns: new[] { "TenantId", "TargetEntityType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfflinePhotoSyncReceipts_ActivityId",
                table: "OfflinePhotoSyncReceipts",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflinePhotoSyncReceipts_CheckpointResponseRevisionId",
                table: "OfflinePhotoSyncReceipts",
                column: "CheckpointResponseRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflinePhotoSyncReceipts_EvidenceId",
                table: "OfflinePhotoSyncReceipts",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflinePhotoSyncReceipts_UserId_IdempotencyKey",
                table: "OfflinePhotoSyncReceipts",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfflineSyncReceipts_ServerActivityId",
                table: "OfflineSyncReceipts",
                column: "ServerActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineSyncReceipts_UserId_ClientActivityId",
                table: "OfflineSyncReceipts",
                columns: new[] { "UserId", "ClientActivityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfflineSyncReceipts_UserId_IdempotencyKey",
                table: "OfflineSyncReceipts",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningApprovalEvents_CampaignId_Revision_OccurredAt",
                table: "PlanningApprovalEvents",
                columns: new[] { "CampaignId", "Revision", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAreaPhotos_ProjectAreaId",
                table: "ProjectAreaPhotos",
                column: "ProjectAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAreaPhotos_UploadedByUserId",
                table: "ProjectAreaPhotos",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAreas_ProjectId_AreaName",
                table: "ProjectAreas",
                columns: new[] { "ProjectId", "AreaName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AssignedClientId",
                table: "Projects",
                column: "AssignedClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AssignedQcId",
                table: "Projects",
                column: "AssignedQcId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AssignedSurveyorId",
                table: "Projects",
                column: "AssignedSurveyorId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedByUserId",
                table: "Projects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_JobNo",
                table: "Projects",
                column: "JobNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OurCompanyId",
                table: "Projects",
                column: "OurCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_QcReviews_ReviewedByUserId",
                table: "QcReviews",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QcReviews_SurveySessionId",
                table: "QcReviews",
                column: "SurveySessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationOtps_Email",
                table: "RegistrationOtps",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportClientRemarks_ClientUserId",
                table: "ReportClientRemarks",
                column: "ClientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportClientRemarks_ProjectId_CreatedAt",
                table: "ReportClientRemarks",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportRevisions_FileId",
                table: "ReportRevisions",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRevisions_JobId_Revision",
                table: "ReportRevisions",
                columns: new[] { "JobId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SamplingApprovals_AnnualListId",
                table: "SamplingApprovals",
                column: "AnnualListId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplingApprovals_CampaignId_AnnualListId_CampaignRevision",
                table: "SamplingApprovals",
                columns: new[] { "CampaignId", "AnnualListId", "CampaignRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyAnswers_ChecklistQuestionId",
                table: "SurveyAnswers",
                column: "ChecklistQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyAnswers_SurveySessionId_ChecklistQuestionId",
                table: "SurveyAnswers",
                columns: new[] { "SurveySessionId", "ChecklistQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveySessions_AreaEquipmentId",
                table: "SurveySessions",
                column: "AreaEquipmentId",
                unique: true,
                filter: "[AreaEquipmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SurveySessions_ChecklistTemplateId",
                table: "SurveySessions",
                column: "ChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveySessions_EquipmentId",
                table: "SurveySessions",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveySessions_JobId",
                table: "SurveySessions",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveySessions_SurveyorId",
                table: "SurveySessions",
                column: "SurveyorId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncBatches_SurveyorId_ClientBatchId",
                table: "SyncBatches",
                columns: new[] { "SurveyorId", "ClientBatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantBackfillExceptions_MigrationBatchId_SourceSystem_SourceEntityType_SourceRecordKey_ExceptionCode",
                table: "TenantBackfillExceptions",
                columns: new[] { "MigrationBatchId", "SourceSystem", "SourceEntityType", "SourceRecordKey", "ExceptionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantBackfillExceptions_TenantId_Status_CreatedAt",
                table: "TenantBackfillExceptions",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantContracts_TenantId_ContractKey",
                table: "TenantContracts",
                columns: new[] { "TenantId", "ContractKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_InvitedByUserId",
                table: "TenantMemberships",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_TenantId_UserId",
                table: "TenantMemberships",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_UserId",
                table: "TenantMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantProjectBindings_ProjectId",
                table: "TenantProjectBindings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantProjectBindings_TenantId_ContractId_ProjectId",
                table: "TenantProjectBindings",
                columns: new[] { "TenantId", "ContractId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantRoleGrants_TenantId_TenantMembershipId_RoleCode_ScopeType",
                table: "TenantRoleGrants",
                columns: new[] { "TenantId", "TenantMembershipId", "RoleCode", "ScopeType" },
                unique: true,
                filter: "[ScopeType] = N'Tenant'");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRoleGrants_TenantId_TenantMembershipId_RoleCode_ScopeType_ScopeId",
                table: "TenantRoleGrants",
                columns: new[] { "TenantId", "TenantMembershipId", "RoleCode", "ScopeType", "ScopeId" },
                unique: true,
                filter: "[ScopeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantKey",
                table: "Tenants",
                column: "TenantKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSites_TenantId_TenantProjectBindingId",
                table: "TenantSites",
                columns: new[] { "TenantId", "TenantProjectBindingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ApprovalReviewedByUserId",
                table: "Users",
                column: "ApprovalReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerId",
                table: "Users",
                column: "ManagerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityReviewEvents");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CampaignEquipment");

            migrationBuilder.DropTable(
                name: "CheckpointReviewEvents");

            migrationBuilder.DropTable(
                name: "EquipmentDocumentRequirementPolicies");

            migrationBuilder.DropTable(
                name: "EquipmentDocumentUseEvents");

            migrationBuilder.DropTable(
                name: "EquipmentReferenceDocumentEvents");

            migrationBuilder.DropTable(
                name: "EquipmentScanEvents");

            migrationBuilder.DropTable(
                name: "EquipmentTagAssignments");

            migrationBuilder.DropTable(
                name: "EquipmentTagHistory");

            migrationBuilder.DropTable(
                name: "GeneratedReportArtifacts");

            migrationBuilder.DropTable(
                name: "InspectionActivityDocumentUsages");

            migrationBuilder.DropTable(
                name: "InspectionRecurrences");

            migrationBuilder.DropTable(
                name: "InspectionTaskAssignmentEvents");

            migrationBuilder.DropTable(
                name: "JobAssignments");

            migrationBuilder.DropTable(
                name: "LegacyEntityMaps");

            migrationBuilder.DropTable(
                name: "OfflinePhotoSyncReceipts");

            migrationBuilder.DropTable(
                name: "OfflineSyncReceipts");

            migrationBuilder.DropTable(
                name: "PlanningApprovalEvents");

            migrationBuilder.DropTable(
                name: "ProjectAreaPhotos");

            migrationBuilder.DropTable(
                name: "QcReviews");

            migrationBuilder.DropTable(
                name: "RegistrationOtps");

            migrationBuilder.DropTable(
                name: "ReportClientRemarks");

            migrationBuilder.DropTable(
                name: "ReportRevisions");

            migrationBuilder.DropTable(
                name: "SamplingApprovals");

            migrationBuilder.DropTable(
                name: "SurveyAnswers");

            migrationBuilder.DropTable(
                name: "SyncBatches");

            migrationBuilder.DropTable(
                name: "TenantBackfillExceptions");

            migrationBuilder.DropTable(
                name: "TenantRoleGrants");

            migrationBuilder.DropTable(
                name: "TenantSites");

            migrationBuilder.DropTable(
                name: "EquipmentReferenceDocuments");

            migrationBuilder.DropTable(
                name: "ActivityEvidence");

            migrationBuilder.DropTable(
                name: "CampaignAnnualLists");

            migrationBuilder.DropTable(
                name: "ChecklistQuestions");

            migrationBuilder.DropTable(
                name: "TenantMemberships");

            migrationBuilder.DropTable(
                name: "TenantProjectBindings");

            migrationBuilder.DropTable(
                name: "CorrectiveActionEvents");

            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "TenantContracts");

            migrationBuilder.DropTable(
                name: "CorrectiveActions");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "ActivityFindings");

            migrationBuilder.DropTable(
                name: "CheckpointResponseRevisions");

            migrationBuilder.DropTable(
                name: "Findings");

            migrationBuilder.DropTable(
                name: "CheckpointDefinitionSnapshots");

            migrationBuilder.DropTable(
                name: "InspectionActivityRevisions");

            migrationBuilder.DropTable(
                name: "InspectionActivities");

            migrationBuilder.DropTable(
                name: "InspectionTasks");

            migrationBuilder.DropTable(
                name: "SurveySessions");

            migrationBuilder.DropTable(
                name: "ChecklistVersionSnapshots");

            migrationBuilder.DropTable(
                name: "InspectionPlans");

            migrationBuilder.DropTable(
                name: "InspectionWorkPackages");

            migrationBuilder.DropTable(
                name: "AreaEquipment");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "ChecklistTemplates");

            migrationBuilder.DropTable(
                name: "InspectionCampaigns");

            migrationBuilder.DropTable(
                name: "ProjectAreas");

            migrationBuilder.DropTable(
                name: "Areas");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "CompanyMasters");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
