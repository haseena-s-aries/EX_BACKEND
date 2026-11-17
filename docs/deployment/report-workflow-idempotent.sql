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

