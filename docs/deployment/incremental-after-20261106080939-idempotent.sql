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

