BEGIN TRANSACTION;
ALTER TABLE [InspectionTasks] ADD [CarryForwardReason] nvarchar(1000) NULL;

ALTER TABLE [InspectionTasks] ADD [ChecklistVersionId] uniqueidentifier NULL;

ALTER TABLE [InspectionTasks] ADD [DueAt] datetimeoffset NULL;

ALTER TABLE [InspectionTasks] ADD [IsInaccessible] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [InspectionTasks] ADD [IsolationPrerequisites] nvarchar(2000) NULL;

ALTER TABLE [InspectionTasks] ADD [PlannedGrade] int NULL;

ALTER TABLE [InspectionTasks] ADD [PriorityCode] nvarchar(max) NULL;

ALTER TABLE [InspectionTasks] ADD [RequiredPermits] nvarchar(2000) NULL;

ALTER TABLE [InspectionTasks] ADD [Rotation] nvarchar(max) NULL;

ALTER TABLE [InspectionTasks] ADD [RouteOrder] int NULL;

ALTER TABLE [InspectionTasks] ADD [WorkPackageId] uniqueidentifier NULL;

CREATE TABLE [InspectionCampaigns] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Reference] nvarchar(100) NOT NULL,
    [StartsOn] date NOT NULL,
    [EndsOn] date NOT NULL,
    [Status] int NOT NULL,
    [ClosePercent] decimal(5,2) NULL,
    [DetailedPercent] decimal(5,2) NULL,
    [MinimumDetailedCreditPercent] decimal(5,2) NULL,
    [RoundingRule] int NULL,
    [Revision] int NOT NULL,
    [AllocationApproved] bit NOT NULL,
    [AllocationApprovedByUserId] uniqueidentifier NULL,
    [AllocationApprovedAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InspectionCampaigns] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionCampaigns_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InspectionTaskAssignmentEvents] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionTaskId] uniqueidentifier NOT NULL,
    [PreviousUserId] uniqueidentifier NULL,
    [NewUserId] uniqueidentifier NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [Reason] nvarchar(1000) NULL,
    [ChangedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_InspectionTaskAssignmentEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionTaskAssignmentEvents_InspectionTasks_InspectionTaskId] FOREIGN KEY ([InspectionTaskId]) REFERENCES [InspectionTasks] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [CampaignAnnualLists] (
    [Id] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [Year] int NOT NULL,
    [EquipmentCount] int NOT NULL,
    [PlannedCloseCount] int NULL,
    [PlannedDetailedCount] int NULL,
    [ActualCloseCount] int NULL,
    [ActualDetailedCount] int NULL,
    [Revision] int NOT NULL,
    CONSTRAINT [PK_CampaignAnnualLists] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CampaignAnnualLists_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InspectionWorkPackages] (
    [Id] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Reference] nvarchar(100) NOT NULL,
    [Site] nvarchar(200) NULL,
    [Area] nvarchar(200) NULL,
    [Rotation] nvarchar(100) NULL,
    [Status] nvarchar(40) NOT NULL,
    [PlannedStart] date NULL,
    [PlannedEnd] date NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_InspectionWorkPackages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionWorkPackages_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InspectionWorkPackages_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [PlanningApprovalEvents] (
    [Id] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [Revision] int NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [Decision] nvarchar(40) NOT NULL,
    [Comments] nvarchar(4000) NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PlanningApprovalEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PlanningApprovalEvents_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [CampaignEquipment] (
    [Id] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [AnnualListId] uniqueidentifier NOT NULL,
    [AreaEquipmentId] uniqueidentifier NOT NULL,
    [RiskCode] nvarchar(max) NULL,
    [RiskScore] decimal(12,4) NULL,
    [EquipmentTypeSnapshot] nvarchar(max) NULL,
    CONSTRAINT [PK_CampaignEquipment] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CampaignEquipment_AreaEquipment_AreaEquipmentId] FOREIGN KEY ([AreaEquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CampaignEquipment_CampaignAnnualLists_AnnualListId] FOREIGN KEY ([AnnualListId]) REFERENCES [CampaignAnnualLists] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CampaignEquipment_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InspectionRecurrences] (
    [Id] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [AnnualListId] uniqueidentifier NOT NULL,
    [Grade] int NOT NULL,
    [EveryMonths] int NOT NULL,
    [NextOccurrenceOn] date NOT NULL,
    [Enabled] bit NOT NULL,
    [LastGeneratedAt] datetimeoffset NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_InspectionRecurrences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionRecurrences_CampaignAnnualLists_AnnualListId] FOREIGN KEY ([AnnualListId]) REFERENCES [CampaignAnnualLists] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InspectionRecurrences_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [SamplingApprovals] (
    [Id] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [AnnualListId] uniqueidentifier NOT NULL,
    [Method] int NOT NULL,
    [RuleVersion] int NOT NULL,
    [RandomSeed] int NULL,
    [Justification] nvarchar(max) NULL,
    [SelectedEquipmentIdsJson] nvarchar(max) NOT NULL,
    [OverridesJson] nvarchar(max) NULL,
    [ApprovedByUserId] uniqueidentifier NULL,
    [SelectedAt] datetimeoffset NOT NULL,
    [ApprovedAt] datetimeoffset NULL,
    [IsFrozen] bit NOT NULL,
    CONSTRAINT [PK_SamplingApprovals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SamplingApprovals_CampaignAnnualLists_AnnualListId] FOREIGN KEY ([AnnualListId]) REFERENCES [CampaignAnnualLists] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SamplingApprovals_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_InspectionTasks_ChecklistVersionId] ON [InspectionTasks] ([ChecklistVersionId]);

CREATE INDEX [IX_InspectionTasks_WorkPackageId_RouteOrder] ON [InspectionTasks] ([WorkPackageId], [RouteOrder]);

CREATE UNIQUE INDEX [IX_CampaignAnnualLists_CampaignId_Year] ON [CampaignAnnualLists] ([CampaignId], [Year]);

CREATE INDEX [IX_CampaignEquipment_AnnualListId] ON [CampaignEquipment] ([AnnualListId]);

CREATE INDEX [IX_CampaignEquipment_AreaEquipmentId] ON [CampaignEquipment] ([AreaEquipmentId]);

CREATE UNIQUE INDEX [IX_CampaignEquipment_CampaignId_AreaEquipmentId] ON [CampaignEquipment] ([CampaignId], [AreaEquipmentId]);

CREATE UNIQUE INDEX [IX_InspectionCampaigns_ProjectId_Reference] ON [InspectionCampaigns] ([ProjectId], [Reference]);

CREATE INDEX [IX_InspectionRecurrences_AnnualListId] ON [InspectionRecurrences] ([AnnualListId]);

CREATE INDEX [IX_InspectionRecurrences_CampaignId_AnnualListId_Grade_NextOccurrenceOn] ON [InspectionRecurrences] ([CampaignId], [AnnualListId], [Grade], [NextOccurrenceOn]);

CREATE INDEX [IX_InspectionTaskAssignmentEvents_InspectionTaskId_ChangedAt] ON [InspectionTaskAssignmentEvents] ([InspectionTaskId], [ChangedAt]);

CREATE UNIQUE INDEX [IX_InspectionWorkPackages_CampaignId_Reference] ON [InspectionWorkPackages] ([CampaignId], [Reference]);

CREATE INDEX [IX_InspectionWorkPackages_ProjectId] ON [InspectionWorkPackages] ([ProjectId]);

CREATE INDEX [IX_PlanningApprovalEvents_CampaignId_Revision_OccurredAt] ON [PlanningApprovalEvents] ([CampaignId], [Revision], [OccurredAt]);

CREATE INDEX [IX_SamplingApprovals_AnnualListId] ON [SamplingApprovals] ([AnnualListId]);

CREATE INDEX [IX_SamplingApprovals_CampaignId] ON [SamplingApprovals] ([CampaignId]);

ALTER TABLE [InspectionTasks] ADD CONSTRAINT [FK_InspectionTasks_ChecklistVersionSnapshots_ChecklistVersionId] FOREIGN KEY ([ChecklistVersionId]) REFERENCES [ChecklistVersionSnapshots] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [InspectionTasks] ADD CONSTRAINT [FK_InspectionTasks_InspectionWorkPackages_WorkPackageId] FOREIGN KEY ([WorkPackageId]) REFERENCES [InspectionWorkPackages] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031043123_InspectionPlanningScheduling', N'10.0.13');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [InspectionCampaigns] ADD [InspectionLeadUserId] uniqueidentifier NULL;

CREATE INDEX [IX_InspectionCampaigns_InspectionLeadUserId] ON [InspectionCampaigns] ([InspectionLeadUserId]);

ALTER TABLE [InspectionCampaigns] ADD CONSTRAINT [FK_InspectionCampaigns_Users_InspectionLeadUserId] FOREIGN KEY ([InspectionLeadUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031044320_InspectionPlanningLeadAssignment', N'10.0.13');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [SamplingApprovals] ADD [CampaignRevision] int NOT NULL DEFAULT 0;

CREATE INDEX [IX_SamplingApprovals_CampaignId_AnnualListId_CampaignRevision] ON [SamplingApprovals] ([CampaignId], [AnnualListId], [CampaignRevision]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031044847_InspectionSamplingCampaignRevision', N'10.0.13');

COMMIT;
GO

