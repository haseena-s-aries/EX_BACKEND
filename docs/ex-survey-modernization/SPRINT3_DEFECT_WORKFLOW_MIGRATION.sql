BEGIN TRANSACTION;
ALTER TABLE [CorrectiveActions] ADD [IsOutstanding] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [CorrectiveActions] ADD [RectificationAt] datetimeoffset NULL;

ALTER TABLE [ActivityFindings] ADD [DueAt] datetimeoffset NULL;

ALTER TABLE [ActivityFindings] ADD [FaultCode] nvarchar(100) NULL;

ALTER TABLE [ActivityFindings] ADD [ReportedAt] datetimeoffset NOT NULL DEFAULT (TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'));

ALTER TABLE [ActivityFindings] ADD [ReportedByUserId] uniqueidentifier NULL;

ALTER TABLE [ActivityFindings] ADD [RequiredMaterialsOrSupport] nvarchar(2000) NULL;

ALTER TABLE [ActivityFindings] ADD [ResponsibleUserId] uniqueidentifier NULL;

CREATE INDEX [IX_ActivityFindings_ReportedByUserId] ON [ActivityFindings] ([ReportedByUserId]);

CREATE INDEX [IX_ActivityFindings_ResponsibleUserId] ON [ActivityFindings] ([ResponsibleUserId]);

ALTER TABLE [ActivityFindings] ADD CONSTRAINT [FK_ActivityFindings_Users_ReportedByUserId] FOREIGN KEY ([ReportedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [ActivityFindings] ADD CONSTRAINT [FK_ActivityFindings_Users_ResponsibleUserId] FOREIGN KEY ([ResponsibleUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030121649_Sprint3DefectWorkflow', N'10.0.13');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [CorrectiveActions] ADD [ResponsibleParty] nvarchar(200) NULL;

ALTER TABLE [ActivityFindings] ADD [ResponsibleParty] nvarchar(200) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030122212_Sprint3DefectResponsibleParty', N'10.0.13');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [CorrectiveActions] ADD [RectificationDescription] nvarchar(4000) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030123924_Sprint3RectificationDescription', N'10.0.13');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [CorrectiveActionEvents] ADD [RectificationAt] datetimeoffset NULL;

ALTER TABLE [CorrectiveActionEvents] ADD [RectificationDescription] nvarchar(4000) NULL;

ALTER TABLE [CorrectiveActionEvents] ADD [ResponsibleParty] nvarchar(200) NULL;

ALTER TABLE [CorrectiveActionEvents] ADD [WorkOrderReference] nvarchar(200) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030124148_Sprint3RectificationAuditHistory', N'10.0.13');

COMMIT;
GO

