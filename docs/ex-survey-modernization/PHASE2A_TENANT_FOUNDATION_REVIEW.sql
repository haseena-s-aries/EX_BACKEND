BEGIN TRANSACTION;
CREATE TABLE [Tenants] (
    [Id] uniqueidentifier NOT NULL,
    [TenantKey] nvarchar(100) NOT NULL,
    [DisplayName] nvarchar(200) NOT NULL,
    [Status] nvarchar(32) NOT NULL DEFAULT N'Provisioning',
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
);

CREATE TABLE [LegacyEntityMaps] (
    [Id] uniqueidentifier NOT NULL,
    [SourceSystem] nvarchar(100) NOT NULL,
    [SourceEntityType] nvarchar(100) NOT NULL,
    [SourceRecordKey] nvarchar(200) NOT NULL,
    [TargetEntityType] nvarchar(100) NULL,
    [TargetId] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NULL,
    [Status] nvarchar(24) NOT NULL DEFAULT N'Proposed',
    [EvidenceReference] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_LegacyEntityMaps] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_LegacyEntityMaps_VerifiedTarget] CHECK ([Status] <> N'Verified' OR ([TargetEntityType] IS NOT NULL AND [TargetId] IS NOT NULL)),
    CONSTRAINT [FK_LegacyEntityMaps_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [TenantBackfillExceptions] (
    [Id] uniqueidentifier NOT NULL,
    [MigrationBatchId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [SourceSystem] nvarchar(100) NOT NULL,
    [SourceEntityType] nvarchar(100) NOT NULL,
    [SourceRecordKey] nvarchar(200) NOT NULL,
    [ExceptionCode] nvarchar(80) NOT NULL,
    [Description] nvarchar(2000) NOT NULL,
    [Status] nvarchar(24) NOT NULL DEFAULT N'Open',
    [EvidenceReference] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ResolvedAt] datetimeoffset NULL,
    CONSTRAINT [PK_TenantBackfillExceptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TenantBackfillExceptions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [TenantContracts] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ContractKey] nvarchar(120) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [StatusCode] nvarchar(40) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_TenantContracts] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_TenantContracts_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_TenantContracts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [TenantMemberships] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Status] nvarchar(32) NOT NULL DEFAULT N'Invited',
    [InvitedByUserId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ActivatedAt] datetimeoffset NULL,
    CONSTRAINT [PK_TenantMemberships] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_TenantMemberships_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_TenantMemberships_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TenantMemberships_Users_InvitedByUserId] FOREIGN KEY ([InvitedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TenantMemberships_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [TenantProjectBindings] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ContractId] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_TenantProjectBindings] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_TenantProjectBindings_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_TenantProjectBindings_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TenantProjectBindings_TenantContracts_TenantId_ContractId] FOREIGN KEY ([TenantId], [ContractId]) REFERENCES [TenantContracts] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [TenantRoleGrants] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [TenantMembershipId] uniqueidentifier NOT NULL,
    [RoleCode] nvarchar(80) NOT NULL,
    [ScopeType] nvarchar(24) NOT NULL,
    [ScopeId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_TenantRoleGrants] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_TenantRoleGrants_ScopeShape] CHECK (([ScopeType] = N'Tenant' AND [ScopeId] IS NULL) OR ([ScopeType] <> N'Tenant' AND [ScopeId] IS NOT NULL)),
    CONSTRAINT [FK_TenantRoleGrants_TenantMemberships_TenantId_TenantMembershipId] FOREIGN KEY ([TenantId], [TenantMembershipId]) REFERENCES [TenantMemberships] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [TenantSites] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [TenantProjectBindingId] uniqueidentifier NOT NULL,
    [SiteKey] nvarchar(120) NULL,
    [Name] nvarchar(200) NOT NULL,
    [SiteType] nvarchar(40) NULL,
    [Location] nvarchar(300) NULL,
    [Status] nvarchar(32) NOT NULL DEFAULT N'Provisioning',
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_TenantSites] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_TenantSites_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_TenantSites_TenantProjectBindings_TenantId_TenantProjectBindingId] FOREIGN KEY ([TenantId], [TenantProjectBindingId]) REFERENCES [TenantProjectBindings] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TenantSites_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_LegacyEntityMaps_SourceSystem_SourceEntityType_SourceRecordKey] ON [LegacyEntityMaps] ([SourceSystem], [SourceEntityType], [SourceRecordKey]);

CREATE INDEX [IX_LegacyEntityMaps_TenantId_TargetEntityType_TargetId] ON [LegacyEntityMaps] ([TenantId], [TargetEntityType], [TargetId]);

CREATE UNIQUE INDEX [IX_TenantBackfillExceptions_MigrationBatchId_SourceSystem_SourceEntityType_SourceRecordKey_ExceptionCode] ON [TenantBackfillExceptions] ([MigrationBatchId], [SourceSystem], [SourceEntityType], [SourceRecordKey], [ExceptionCode]);

CREATE INDEX [IX_TenantBackfillExceptions_TenantId_Status_CreatedAt] ON [TenantBackfillExceptions] ([TenantId], [Status], [CreatedAt]);

CREATE UNIQUE INDEX [IX_TenantContracts_TenantId_ContractKey] ON [TenantContracts] ([TenantId], [ContractKey]);

CREATE INDEX [IX_TenantMemberships_InvitedByUserId] ON [TenantMemberships] ([InvitedByUserId]);

CREATE UNIQUE INDEX [IX_TenantMemberships_TenantId_UserId] ON [TenantMemberships] ([TenantId], [UserId]);

CREATE INDEX [IX_TenantMemberships_UserId] ON [TenantMemberships] ([UserId]);

CREATE INDEX [IX_TenantProjectBindings_ProjectId] ON [TenantProjectBindings] ([ProjectId]);

CREATE UNIQUE INDEX [IX_TenantProjectBindings_TenantId_ContractId_ProjectId] ON [TenantProjectBindings] ([TenantId], [ContractId], [ProjectId]);

CREATE UNIQUE INDEX [IX_TenantRoleGrants_TenantId_TenantMembershipId_RoleCode_ScopeType_ScopeId] ON [TenantRoleGrants] ([TenantId], [TenantMembershipId], [RoleCode], [ScopeType], [ScopeId]) WHERE [ScopeId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_TenantRoleGrants_TenantId_TenantMembershipId_RoleCode_ScopeType] ON [TenantRoleGrants] ([TenantId], [TenantMembershipId], [RoleCode], [ScopeType]) WHERE [ScopeType] = N'Tenant';

CREATE UNIQUE INDEX [IX_Tenants_TenantKey] ON [Tenants] ([TenantKey]);

CREATE INDEX [IX_TenantSites_TenantId_TenantProjectBindingId] ON [TenantSites] ([TenantId], [TenantProjectBindingId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029123545_Phase2aTenantFoundation', N'10.0.13');

COMMIT;
GO

