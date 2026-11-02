BEGIN TRANSACTION;
CREATE TABLE [EquipmentTagAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [EquipmentId] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Technology] nvarchar(30) NOT NULL,
    [NormalizedIdentifier] nvarchar(200) NOT NULL,
    [IsActive] bit NOT NULL,
    [AssignedByUserId] uniqueidentifier NOT NULL,
    [AssignedAt] datetimeoffset NOT NULL,
    [AssignmentReason] nvarchar(1000) NOT NULL,
    [DeactivatedAt] datetimeoffset NULL,
    [DeactivatedByUserId] uniqueidentifier NULL,
    [DeactivationReason] nvarchar(1000) NULL,
    CONSTRAINT [PK_EquipmentTagAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EquipmentTagAssignments_AreaEquipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EquipmentTagAssignments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EquipmentTagAssignments_Users_AssignedByUserId] FOREIGN KEY ([AssignedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EquipmentTagAssignments_Users_DeactivatedByUserId] FOREIGN KEY ([DeactivatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [EquipmentTagHistory] (
    [Id] uniqueidentifier NOT NULL,
    [EquipmentId] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [AssignmentId] uniqueidentifier NULL,
    [Technology] nvarchar(30) NOT NULL,
    [NormalizedIdentifier] nvarchar(200) NOT NULL,
    [ActionCode] nvarchar(40) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EquipmentTagHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EquipmentTagHistory_AreaEquipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EquipmentTagHistory_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EquipmentTagHistory_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

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
    CONSTRAINT [FK_GeneratedReportArtifacts_Users_GeneratedByUserId] FOREIGN KEY ([GeneratedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_EquipmentTagAssignments_AssignedByUserId] ON [EquipmentTagAssignments] ([AssignedByUserId]);

CREATE INDEX [IX_EquipmentTagAssignments_DeactivatedByUserId] ON [EquipmentTagAssignments] ([DeactivatedByUserId]);

CREATE UNIQUE INDEX [IX_EquipmentTagAssignments_EquipmentId_Technology] ON [EquipmentTagAssignments] ([EquipmentId], [Technology]) WHERE [IsActive] = 1;

CREATE UNIQUE INDEX [IX_EquipmentTagAssignments_NormalizedIdentifier] ON [EquipmentTagAssignments] ([NormalizedIdentifier]) WHERE [IsActive] = 1;

CREATE INDEX [IX_EquipmentTagAssignments_ProjectId] ON [EquipmentTagAssignments] ([ProjectId]);

CREATE INDEX [IX_EquipmentTagHistory_ActorUserId] ON [EquipmentTagHistory] ([ActorUserId]);

CREATE INDEX [IX_EquipmentTagHistory_AssignmentId] ON [EquipmentTagHistory] ([AssignmentId]);

CREATE INDEX [IX_EquipmentTagHistory_EquipmentId_OccurredAt] ON [EquipmentTagHistory] ([EquipmentId], [OccurredAt]);

CREATE INDEX [IX_EquipmentTagHistory_ProjectId] ON [EquipmentTagHistory] ([ProjectId]);

CREATE INDEX [IX_GeneratedReportArtifacts_GeneratedByUserId] ON [GeneratedReportArtifacts] ([GeneratedByUserId]);

CREATE INDEX [IX_GeneratedReportArtifacts_ProjectId_GeneratedAt] ON [GeneratedReportArtifacts] ([ProjectId], [GeneratedAt]);

CREATE UNIQUE INDEX [IX_GeneratedReportArtifacts_ProjectId_ReportType_LinkedEntityId_Revision] ON [GeneratedReportArtifacts] ([ProjectId], [ReportType], [LinkedEntityId], [Revision]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031063837_EquipmentTagAssignmentsAndReportArchive', N'10.0.13');

COMMIT;
GO

