BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    DROP INDEX [IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber] ON [ChecklistVersionSnapshots];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [CheckpointDefinitionSnapshots] ADD [RequireRemarkOnFail] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [CheckpointDefinitionSnapshots] ADD [SectionCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistVersionSnapshots] ADD [ControlledSourceReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistVersionSnapshots] ADD [ProtectionTypeSnapshot] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistVersionSnapshots] ADD [StandardEdition] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistVersionSnapshots] ADD [StandardReference] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [ApplicableProtectionTypes] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [ApprovedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [ApprovedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [ControlledSourceReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [CreatedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [DraftRevision] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [FamilyId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [LifecycleStatus] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [StandardEdition] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [StandardReference] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [SubmittedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [SubmittedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [ApplicableProtectionTypes] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [RequireRemarkOnFail] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [ResponseScaleCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [SectionCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [AmbientTemperatureRange] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [Description] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [ExProtectionType] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [GasGroup] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [HazardousAreaZone] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [IpRating] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [Model] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [NotApplicableFieldsJson] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [NotifiedBody] nvarchar(250) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [ReconciliationStatus] nvarchar(50) NOT NULL DEFAULT N'Unreviewed';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [RfidIdentificationMethod] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [SerialNumber] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [SpecialConditionsOfUse] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [TemperatureClass] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber_ProtectionTypeSnapshot] ON [ChecklistVersionSnapshots] ([SourceChecklistTemplateId], [VersionNumber], [ProtectionTypeSnapshot]) WHERE [ProtectionTypeSnapshot] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    CREATE INDEX [IX_ChecklistTemplates_ApprovedByUserId] ON [ChecklistTemplates] ([ApprovedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    CREATE INDEX [IX_ChecklistTemplates_CreatedByUserId] ON [ChecklistTemplates] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChecklistTemplates_FamilyId_Version] ON [ChecklistTemplates] ([FamilyId], [Version]) WHERE [FamilyId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    CREATE INDEX [IX_ChecklistTemplates_SubmittedByUserId] ON [ChecklistTemplates] ([SubmittedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD CONSTRAINT [FK_ChecklistTemplates_Users_ApprovedByUserId] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD CONSTRAINT [FK_ChecklistTemplates_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD CONSTRAINT [FK_ChecklistTemplates_Users_SubmittedByUserId] FOREIGN KEY ([SubmittedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030131820_ChecklistAdminAndCompleteHaer', N'10.0.13');
END;

COMMIT;
GO

