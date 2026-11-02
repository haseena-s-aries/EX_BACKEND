BEGIN TRANSACTION;
CREATE TABLE [GeneratedReportArtifacts] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [LinkedEntityId] uniqueidentifier NOT NULL,
    [ReportType] nvarchar(40) NOT NULL,
    [ReportNumber] nvarchar(160) NOT NULL,
    [Revision] int NOT NULL,
    [FileName] nvarchar(255) NOT NULL,
    [ContentType] nvarchar(150) NOT NULL,
    [StorageKey] nvarchar(1000) NOT NULL,
    [Sha256] nvarchar(64) NOT NULL,
    [GeneratedAt] datetimeoffset NOT NULL,
    [GeneratedByUserId] uniqueidentifier NOT NULL,
    [ApprovalState] nvarchar(30) NOT NULL,
    CONSTRAINT [PK_GeneratedReportArtifacts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GeneratedReportArtifacts_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GeneratedReportArtifacts_Users_GeneratedByUserId] FOREIGN KEY ([GeneratedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_GeneratedReportArtifacts_GeneratedByUserId] ON [GeneratedReportArtifacts] ([GeneratedByUserId]);

CREATE INDEX [IX_GeneratedReportArtifacts_ProjectId_GeneratedAt] ON [GeneratedReportArtifacts] ([ProjectId], [GeneratedAt]);

CREATE UNIQUE INDEX [IX_GeneratedReportArtifacts_ProjectId_ReportType_LinkedEntityId_Revision] ON [GeneratedReportArtifacts] ([ProjectId], [ReportType], [LinkedEntityId], [Revision]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031063513_ArchiveGeneratedReportArtifacts', N'10.0.13');

COMMIT;
GO

