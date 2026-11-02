BEGIN TRANSACTION;
CREATE TABLE [OfflinePhotoSyncReceipts] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [IdempotencyKey] nvarchar(100) NOT NULL,
    [ContentHash] nvarchar(64) NOT NULL,
    [ActivityId] uniqueidentifier NOT NULL,
    [CheckpointResponseRevisionId] uniqueidentifier NULL,
    [EvidenceId] uniqueidentifier NOT NULL,
    [AcceptedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_OfflinePhotoSyncReceipts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OfflinePhotoSyncReceipts_ActivityEvidence_EvidenceId] FOREIGN KEY ([EvidenceId]) REFERENCES [ActivityEvidence] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OfflinePhotoSyncReceipts_CheckpointResponseRevisions_CheckpointResponseRevisionId] FOREIGN KEY ([CheckpointResponseRevisionId]) REFERENCES [CheckpointResponseRevisions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OfflinePhotoSyncReceipts_InspectionActivities_ActivityId] FOREIGN KEY ([ActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OfflinePhotoSyncReceipts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [OfflineSyncReceipts] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ClientActivityId] uniqueidentifier NOT NULL,
    [IdempotencyKey] nvarchar(100) NOT NULL,
    [PayloadHash] nvarchar(64) NOT NULL,
    [ServerActivityId] uniqueidentifier NOT NULL,
    [ResponseMapJson] nvarchar(max) NOT NULL,
    [AcceptedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_OfflineSyncReceipts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OfflineSyncReceipts_InspectionActivities_ServerActivityId] FOREIGN KEY ([ServerActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OfflineSyncReceipts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_OfflinePhotoSyncReceipts_ActivityId] ON [OfflinePhotoSyncReceipts] ([ActivityId]);

CREATE INDEX [IX_OfflinePhotoSyncReceipts_CheckpointResponseRevisionId] ON [OfflinePhotoSyncReceipts] ([CheckpointResponseRevisionId]);

CREATE INDEX [IX_OfflinePhotoSyncReceipts_EvidenceId] ON [OfflinePhotoSyncReceipts] ([EvidenceId]);

CREATE UNIQUE INDEX [IX_OfflinePhotoSyncReceipts_UserId_IdempotencyKey] ON [OfflinePhotoSyncReceipts] ([UserId], [IdempotencyKey]);

CREATE INDEX [IX_OfflineSyncReceipts_ServerActivityId] ON [OfflineSyncReceipts] ([ServerActivityId]);

CREATE UNIQUE INDEX [IX_OfflineSyncReceipts_UserId_ClientActivityId] ON [OfflineSyncReceipts] ([UserId], [ClientActivityId]);

CREATE UNIQUE INDEX [IX_OfflineSyncReceipts_UserId_IdempotencyKey] ON [OfflineSyncReceipts] ([UserId], [IdempotencyKey]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031052351_OfflineInspectionSyncReceipts', N'10.0.13');

COMMIT;
GO

