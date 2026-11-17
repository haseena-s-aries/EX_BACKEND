-- ExSurveyApp: upgrade the supplied 11-Nov-2026 backup baseline to the current backend.
-- Run in SSMS with Query > SQLCMD Mode enabled, or use sqlcmd -b.
-- Take a fresh live backup first. This file changes ONLY ExSurveyApp on the connected server.
-- Pending EF migrations are applied in order; recorded migrations are skipped.
-- No database, application table, or historical inspection record is dropped.
:On Error exit
USE [ExSurveyApp];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'ExSurveyApp'
    THROW 51000, 'Wrong database. Expected ExSurveyApp.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 51000, 'Migration history missing. Stop and review the schema.', 1;
IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId IN (
 N'20261103074858_InitialCreate',
 N'20261103102529_ChecklistSourceMetadata',
 N'20261104110245_EquipmentIdentificationName',
 N'20261104115125_EquipmentDefectRegister')) <> 4
    THROW 51000, 'Expected four baseline migrations are not all recorded. Stop and review.', 1;
SELECT @@SERVERNAME AS TargetServer, DB_NAME() AS TargetDatabase;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111559_CategoryInspectionChecklist'
)
BEGIN
    DROP INDEX [IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber_ProtectionTypeSnapshot] ON [ChecklistVersionSnapshots];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111559_CategoryInspectionChecklist'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InspectionActivities]') AND [c].[name] = N'Grade');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [InspectionActivities] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [InspectionActivities] ALTER COLUMN [Grade] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111559_CategoryInspectionChecklist'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChecklistVersionSnapshots]') AND [c].[name] = N'SourceChecklistTemplateId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ChecklistVersionSnapshots] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [ChecklistVersionSnapshots] ALTER COLUMN [SourceChecklistTemplateId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111559_CategoryInspectionChecklist'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChecklistVersionSnapshots]') AND [c].[name] = N'InspectionType');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ChecklistVersionSnapshots] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [ChecklistVersionSnapshots] ALTER COLUMN [InspectionType] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111559_CategoryInspectionChecklist'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber_ProtectionTypeSnapshot] ON [ChecklistVersionSnapshots] ([SourceChecklistTemplateId], [VersionNumber], [ProtectionTypeSnapshot]) WHERE [SourceChecklistTemplateId] IS NOT NULL AND [ProtectionTypeSnapshot] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105111559_CategoryInspectionChecklist'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105111559_CategoryInspectionChecklist', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133840_EquipmentChecklistCatalog'
)
BEGIN
    CREATE TABLE [EquipmentChecklistCatalogs] (
        [Id] int NOT NULL,
        [Version] int NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_EquipmentChecklistCatalogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133840_EquipmentChecklistCatalog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105133840_EquipmentChecklistCatalog', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106060624_InspectionGradeResults'
)
BEGIN
    ALTER TABLE [CheckpointResponseRevisions] ADD [GradeResultsJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106060624_InspectionGradeResults'
)
BEGIN
    ALTER TABLE [CheckpointResponseRevisions] ADD [InspectionComment] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106060624_InspectionGradeResults'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106060624_InspectionGradeResults', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106061852_EquipmentInspectionComments'
)
BEGIN
    ALTER TABLE [InspectionActivities] ADD [EquipmentComments] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106061852_EquipmentInspectionComments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106061852_EquipmentInspectionComments', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106080939_InspectionDefectRegister'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [AosdId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106080939_InspectionDefectRegister'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [FailedGrades] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106080939_InspectionDefectRegister'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [FaultCategory] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106080939_InspectionDefectRegister'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [FollowUpRemarks] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106080939_InspectionDefectRegister'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [RegisterRemarks] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106080939_InspectionDefectRegister'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106080939_InspectionDefectRegister', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106131959_ReportDocumentDetails'
)
BEGIN
    ALTER TABLE [GeneratedReportArtifacts] ADD [DocumentNumber] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106131959_ReportDocumentDetails'
)
BEGIN
    ALTER TABLE [GeneratedReportArtifacts] ADD [DocumentRevision] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106131959_ReportDocumentDetails'
)
BEGIN
    ALTER TABLE [GeneratedReportArtifacts] ADD [UidNumber] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106131959_ReportDocumentDetails'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106131959_ReportDocumentDetails', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109043002_ProjectSurveyorHandover'
)
BEGIN
    ALTER TABLE [Projects] ADD [AdditionalSurveyorId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109043002_ProjectSurveyorHandover'
)
BEGIN
    ALTER TABLE [InspectionActivities] ADD [LastModifiedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109043002_ProjectSurveyorHandover'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [LastModifiedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109043002_ProjectSurveyorHandover'
)
BEGIN
    CREATE INDEX [IX_Projects_AdditionalSurveyorId] ON [Projects] ([AdditionalSurveyorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109043002_ProjectSurveyorHandover'
)
BEGIN
    ALTER TABLE [Projects] ADD CONSTRAINT [FK_Projects_Users_AdditionalSurveyorId] FOREIGN KEY ([AdditionalSurveyorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109043002_ProjectSurveyorHandover'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109043002_ProjectSurveyorHandover', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060233_ProjectWorkSchedule'
)
BEGIN
    ALTER TABLE [Projects] ADD [TargetDate] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060233_ProjectWorkSchedule'
)
BEGIN
    CREATE TABLE [ProjectScheduleEntries] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NULL,
        [Kind] nvarchar(24) NOT NULL,
        [EventKey] nvarchar(100) NOT NULL,
        [TargetDate] date NULL,
        [PreviousTargetDate] date NULL,
        [WorkStartDate] date NULL,
        [ExpectedWorkEndDate] date NULL,
        [DelayReason] nvarchar(2000) NULL,
        [DelayDays] int NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProjectScheduleEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectScheduleEntries_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060233_ProjectWorkSchedule'
)
BEGIN
    CREATE TABLE [ProjectDelayEmails] (
        [Id] uniqueidentifier NOT NULL,
        [ScheduleEntryId] uniqueidentifier NOT NULL,
        [RecipientId] uniqueidentifier NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [AttemptedAt] datetimeoffset NULL,
        [AcceptedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ProjectDelayEmails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectDelayEmails_ProjectScheduleEntries_ScheduleEntryId] FOREIGN KEY ([ScheduleEntryId]) REFERENCES [ProjectScheduleEntries] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060233_ProjectWorkSchedule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectDelayEmails_ScheduleEntryId_RecipientId] ON [ProjectDelayEmails] ([ScheduleEntryId], [RecipientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060233_ProjectWorkSchedule'
)
BEGIN
    CREATE INDEX [IX_ProjectDelayEmails_Status] ON [ProjectDelayEmails] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060233_ProjectWorkSchedule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectScheduleEntries_ProjectId_EventKey] ON [ProjectScheduleEntries] ([ProjectId], [EventKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060233_ProjectWorkSchedule'
)
BEGIN
    CREATE INDEX [IX_ProjectScheduleEntries_ProjectId_Kind_OccurredAt] ON [ProjectScheduleEntries] ([ProjectId], [Kind], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109060233_ProjectWorkSchedule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109060233_ProjectWorkSchedule', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110105419_ProjectAdditionalQc'
)
BEGIN
    ALTER TABLE [Projects] ADD [AdditionalQcId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110105419_ProjectAdditionalQc'
)
BEGIN
    CREATE INDEX [IX_Projects_AdditionalQcId] ON [Projects] ([AdditionalQcId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110105419_ProjectAdditionalQc'
)
BEGIN
    ALTER TABLE [Projects] ADD CONSTRAINT [FK_Projects_Users_AdditionalQcId] FOREIGN KEY ([AdditionalQcId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110105419_ProjectAdditionalQc'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110105419_ProjectAdditionalQc', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110113353_InspectionActivityHold'
)
BEGIN
    ALTER TABLE [InspectionActivities] ADD [IsOnHold] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110113353_InspectionActivityHold'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110113353_InspectionActivityHold', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111054419_ProjectReportStatusWorkflow'
)
BEGIN
    CREATE TABLE [ProjectReportWorkflows] (
        [ProjectId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Ongoing',
        [Revision] int NOT NULL,
        [Version] bigint NOT NULL,
        [HoldReason] nvarchar(2000) NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [UpdatedByUserId] uniqueidentifier NOT NULL,
        [SubmittedAt] datetimeoffset NULL,
        [SubmittedByUserId] uniqueidentifier NULL,
        [SubmittedArtifactId] uniqueidentifier NULL,
        CONSTRAINT [PK_ProjectReportWorkflows] PRIMARY KEY ([ProjectId]),
        CONSTRAINT [FK_ProjectReportWorkflows_GeneratedReportArtifacts_SubmittedArtifactId] FOREIGN KEY ([SubmittedArtifactId]) REFERENCES [GeneratedReportArtifacts] ([Id]),
        CONSTRAINT [FK_ProjectReportWorkflows_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111054419_ProjectReportStatusWorkflow'
)
BEGIN
    CREATE TABLE [ReportWorkflowEvents] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [FromStatus] nvarchar(20) NOT NULL,
        [ToStatus] nvarchar(20) NOT NULL,
        [Revision] int NOT NULL,
        [Version] bigint NOT NULL,
        [Reason] nvarchar(2000) NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        [ArtifactId] uniqueidentifier NULL,
        CONSTRAINT [PK_ReportWorkflowEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportWorkflowEvents_GeneratedReportArtifacts_ArtifactId] FOREIGN KEY ([ArtifactId]) REFERENCES [GeneratedReportArtifacts] ([Id]),
        CONSTRAINT [FK_ReportWorkflowEvents_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111054419_ProjectReportStatusWorkflow'
)
BEGIN
    CREATE INDEX [IX_ProjectReportWorkflows_SubmittedArtifactId] ON [ProjectReportWorkflows] ([SubmittedArtifactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111054419_ProjectReportStatusWorkflow'
)
BEGIN
    CREATE INDEX [IX_ReportWorkflowEvents_ArtifactId] ON [ReportWorkflowEvents] ([ArtifactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111054419_ProjectReportStatusWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportWorkflowEvents_ProjectId_Version] ON [ReportWorkflowEvents] ([ProjectId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111054419_ProjectReportStatusWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111054419_ProjectReportStatusWorkflow', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045739_ProjectMultipleClients'
)
BEGIN
    CREATE TABLE [ProjectClientAssignments] (
        [ProjectId] uniqueidentifier NOT NULL,
        [ClientUserId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [AssignedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProjectClientAssignments] PRIMARY KEY ([ProjectId], [ClientUserId]),
        CONSTRAINT [FK_ProjectClientAssignments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectClientAssignments_Users_ClientUserId] FOREIGN KEY ([ClientUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045739_ProjectMultipleClients'
)
BEGIN
    CREATE INDEX [IX_ProjectClientAssignments_ClientUserId] ON [ProjectClientAssignments] ([ClientUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045739_ProjectMultipleClients'
)
BEGIN
    INSERT INTO [ProjectClientAssignments] ([ProjectId], [ClientUserId], [IsActive], [AssignedAt], [UpdatedAt])
                    SELECT p.[Id], p.[AssignedClientId], CAST(1 AS bit), p.[CreatedAt], p.[UpdatedAt]
                    FROM [Projects] p
                    WHERE p.[AssignedClientId] IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM [ProjectClientAssignments] a WHERE a.[ProjectId] = p.[Id] AND a.[ClientUserId] = p.[AssignedClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045739_ProjectMultipleClients'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112045739_ProjectMultipleClients', N'10.0.13');
END;

COMMIT;
GO


-- Final verification: all sixteen migrations should now be recorded.
SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS TableName
FROM sys.tables
WHERE name IN (N'EquipmentChecklistCatalogs', N'ProjectClientAssignments',
 N'ProjectDelayEmails', N'ProjectReportWorkflows', N'ProjectScheduleEntries', N'ReportWorkflowEvents')
ORDER BY name;
