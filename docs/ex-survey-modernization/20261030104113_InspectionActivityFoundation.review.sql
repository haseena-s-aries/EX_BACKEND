BEGIN TRANSACTION;
CREATE TABLE [ChecklistVersionSnapshots] (
    [Id] uniqueidentifier NOT NULL,
    [SourceChecklistTemplateId] uniqueidentifier NOT NULL,
    [VersionNumber] int NOT NULL,
    [InspectionType] int NOT NULL,
    [SnapshotHash] nvarchar(128) NOT NULL,
    [PublishedAt] datetimeoffset NOT NULL,
    [PublishedByUserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ChecklistVersionSnapshots] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChecklistVersionSnapshots_ChecklistTemplates_SourceChecklistTemplateId] FOREIGN KEY ([SourceChecklistTemplateId]) REFERENCES [ChecklistTemplates] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChecklistVersionSnapshots_Users_PublishedByUserId] FOREIGN KEY ([PublishedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InspectionPlans] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NULL,
    [Name] nvarchar(200) NOT NULL,
    [SourceReference] nvarchar(200) NULL,
    [StartsAt] datetimeoffset NULL,
    [EndsAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_InspectionPlans] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionPlans_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [CheckpointDefinitionSnapshots] (
    [Id] uniqueidentifier NOT NULL,
    [ChecklistVersionSnapshotId] uniqueidentifier NOT NULL,
    [QuestionCode] nvarchar(100) NOT NULL,
    [QuestionVersion] int NOT NULL,
    [QuestionText] nvarchar(4000) NOT NULL,
    [SortOrder] int NOT NULL,
    [ApplicabilityCode] nvarchar(100) NULL,
    [ResponseScaleCode] nvarchar(100) NULL,
    [IsRequired] bit NOT NULL,
    CONSTRAINT [PK_CheckpointDefinitionSnapshots] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CheckpointDefinitionSnapshots_ChecklistVersionSnapshots_ChecklistVersionSnapshotId] FOREIGN KEY ([ChecklistVersionSnapshotId]) REFERENCES [ChecklistVersionSnapshots] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InspectionTasks] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionPlanId] uniqueidentifier NOT NULL,
    [AreaEquipmentId] uniqueidentifier NULL,
    [LegacyEquipmentId] uniqueidentifier NULL,
    [Kind] int NOT NULL,
    [ScheduleReference] nvarchar(200) NULL,
    [WorkPackageReference] nvarchar(200) NULL,
    [AssignedUserId] uniqueidentifier NULL,
    [ScheduledAt] datetimeoffset NULL,
    [StatusCode] nvarchar(40) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_InspectionTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_InspectionTasks_OneEquipment] CHECK (([AreaEquipmentId] IS NOT NULL AND [LegacyEquipmentId] IS NULL) OR ([AreaEquipmentId] IS NULL AND [LegacyEquipmentId] IS NOT NULL)),
    CONSTRAINT [FK_InspectionTasks_AreaEquipment_AreaEquipmentId] FOREIGN KEY ([AreaEquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InspectionTasks_Equipment_LegacyEquipmentId] FOREIGN KEY ([LegacyEquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InspectionTasks_InspectionPlans_InspectionPlanId] FOREIGN KEY ([InspectionPlanId]) REFERENCES [InspectionPlans] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InspectionTasks_Users_AssignedUserId] FOREIGN KEY ([AssignedUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InspectionActivities] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionTaskId] uniqueidentifier NOT NULL,
    [LegacySurveySessionId] uniqueidentifier NULL,
    [Grade] int NOT NULL,
    [InspectorUserId] uniqueidentifier NOT NULL,
    [InspectorNameSnapshot] nvarchar(200) NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [CompletedAt] datetimeoffset NULL,
    [ChecklistVersionId] uniqueidentifier NOT NULL,
    [ReviewStatus] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_InspectionActivities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionActivities_ChecklistVersionSnapshots_ChecklistVersionId] FOREIGN KEY ([ChecklistVersionId]) REFERENCES [ChecklistVersionSnapshots] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InspectionActivities_InspectionTasks_InspectionTaskId] FOREIGN KEY ([InspectionTaskId]) REFERENCES [InspectionTasks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InspectionActivities_SurveySessions_LegacySurveySessionId] FOREIGN KEY ([LegacySurveySessionId]) REFERENCES [SurveySessions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InspectionActivities_Users_InspectorUserId] FOREIGN KEY ([InspectorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ActivityReviewEvents] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionActivityId] uniqueidentifier NOT NULL,
    [Decision] int NOT NULL,
    [Comments] nvarchar(4000) NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [DecidedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ActivityReviewEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ActivityReviewEvents_InspectionActivities_InspectionActivityId] FOREIGN KEY ([InspectionActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ActivityReviewEvents_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InspectionActivityRevisions] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionActivityId] uniqueidentifier NOT NULL,
    [RevisionNumber] int NOT NULL,
    [ChangeReason] nvarchar(1000) NULL,
    [ContentHash] nvarchar(128) NOT NULL,
    [CreatedByUserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [InspectionActivityId1] uniqueidentifier NULL,
    CONSTRAINT [PK_InspectionActivityRevisions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InspectionActivityRevisions_InspectionActivities_InspectionActivityId] FOREIGN KEY ([InspectionActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InspectionActivityRevisions_InspectionActivities_InspectionActivityId1] FOREIGN KEY ([InspectionActivityId1]) REFERENCES [InspectionActivities] ([Id]),
    CONSTRAINT [FK_InspectionActivityRevisions_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [CheckpointResponseRevisions] (
    [Id] uniqueidentifier NOT NULL,
    [ActivityRevisionId] uniqueidentifier NOT NULL,
    [CheckpointDefinitionSnapshotId] uniqueidentifier NOT NULL,
    [RevisionNumber] int NOT NULL,
    [ApplicabilityResultCode] nvarchar(100) NULL,
    [ResponseCode] nvarchar(100) NULL,
    [TechnicalConditionCode] nvarchar(100) NULL,
    [WorkflowState] int NOT NULL,
    [Observation] nvarchar(4000) NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [RecordedByUserId] uniqueidentifier NOT NULL,
    [ReviewedByUserId] uniqueidentifier NULL,
    [ReviewedAt] datetimeoffset NULL,
    [ReviewComment] nvarchar(4000) NULL,
    CONSTRAINT [PK_CheckpointResponseRevisions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CheckpointResponseRevisions_CheckpointDefinitionSnapshots_CheckpointDefinitionSnapshotId] FOREIGN KEY ([CheckpointDefinitionSnapshotId]) REFERENCES [CheckpointDefinitionSnapshots] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CheckpointResponseRevisions_InspectionActivityRevisions_ActivityRevisionId] FOREIGN KEY ([ActivityRevisionId]) REFERENCES [InspectionActivityRevisions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CheckpointResponseRevisions_Users_RecordedByUserId] FOREIGN KEY ([RecordedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CheckpointResponseRevisions_Users_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ActivityFindings] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionActivityId] uniqueidentifier NOT NULL,
    [CheckpointResponseRevisionId] uniqueidentifier NOT NULL,
    [LegacyFindingId] uniqueidentifier NULL,
    [Observation] nvarchar(4000) NOT NULL,
    [ProposedAction] nvarchar(4000) NULL,
    [SeverityCode] nvarchar(100) NULL,
    [StatusCode] nvarchar(40) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ActivityFindings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ActivityFindings_CheckpointResponseRevisions_CheckpointResponseRevisionId] FOREIGN KEY ([CheckpointResponseRevisionId]) REFERENCES [CheckpointResponseRevisions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ActivityFindings_Findings_LegacyFindingId] FOREIGN KEY ([LegacyFindingId]) REFERENCES [Findings] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ActivityFindings_InspectionActivities_InspectionActivityId] FOREIGN KEY ([InspectionActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [CorrectiveActions] (
    [Id] uniqueidentifier NOT NULL,
    [ActivityFindingId] uniqueidentifier NOT NULL,
    [ActionReference] nvarchar(100) NOT NULL,
    [SupplementaryInformation] nvarchar(4000) NULL,
    [ProposedAction] nvarchar(4000) NULL,
    [ExternalWorkOrderReference] nvarchar(200) NULL,
    [ResponsibleUserId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [AriesVerifierUserId] uniqueidentifier NULL,
    [AriesVerifiedAt] datetimeoffset NULL,
    [ClientDecisionUserId] uniqueidentifier NULL,
    [ClientDecisionAt] datetimeoffset NULL,
    [LastDecisionComment] nvarchar(4000) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CorrectiveActions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CorrectiveActions_ActivityFindings_ActivityFindingId] FOREIGN KEY ([ActivityFindingId]) REFERENCES [ActivityFindings] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CorrectiveActions_Users_AriesVerifierUserId] FOREIGN KEY ([AriesVerifierUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CorrectiveActions_Users_ClientDecisionUserId] FOREIGN KEY ([ClientDecisionUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CorrectiveActions_Users_ResponsibleUserId] FOREIGN KEY ([ResponsibleUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [CorrectiveActionEvents] (
    [Id] uniqueidentifier NOT NULL,
    [CorrectiveActionId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [ResultingStatus] int NOT NULL,
    [ActorUserId] uniqueidentifier NOT NULL,
    [Comment] nvarchar(4000) NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CorrectiveActionEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CorrectiveActionEvents_CorrectiveActions_CorrectiveActionId] FOREIGN KEY ([CorrectiveActionId]) REFERENCES [CorrectiveActions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CorrectiveActionEvents_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ActivityEvidence] (
    [Id] uniqueidentifier NOT NULL,
    [SurveyFileId] uniqueidentifier NOT NULL,
    [CheckpointResponseRevisionId] uniqueidentifier NULL,
    [CorrectiveActionEventId] uniqueidentifier NULL,
    [Role] int NOT NULL,
    [Caption] nvarchar(1000) NULL,
    [IntegrityHash] nvarchar(128) NULL,
    [AddedByUserId] uniqueidentifier NOT NULL,
    [AddedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ActivityEvidence] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ActivityEvidence_CheckpointResponseRevisions_CheckpointResponseRevisionId] FOREIGN KEY ([CheckpointResponseRevisionId]) REFERENCES [CheckpointResponseRevisions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ActivityEvidence_CorrectiveActionEvents_CorrectiveActionEventId] FOREIGN KEY ([CorrectiveActionEventId]) REFERENCES [CorrectiveActionEvents] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ActivityEvidence_Files_SurveyFileId] FOREIGN KEY ([SurveyFileId]) REFERENCES [Files] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ActivityEvidence_Users_AddedByUserId] FOREIGN KEY ([AddedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ActivityEvidence_AddedByUserId] ON [ActivityEvidence] ([AddedByUserId]);

CREATE INDEX [IX_ActivityEvidence_CheckpointResponseRevisionId] ON [ActivityEvidence] ([CheckpointResponseRevisionId]);

CREATE INDEX [IX_ActivityEvidence_CorrectiveActionEventId] ON [ActivityEvidence] ([CorrectiveActionEventId]);

CREATE INDEX [IX_ActivityEvidence_SurveyFileId] ON [ActivityEvidence] ([SurveyFileId]);

CREATE INDEX [IX_ActivityFindings_CheckpointResponseRevisionId] ON [ActivityFindings] ([CheckpointResponseRevisionId]);

CREATE INDEX [IX_ActivityFindings_InspectionActivityId] ON [ActivityFindings] ([InspectionActivityId]);

CREATE UNIQUE INDEX [IX_ActivityFindings_LegacyFindingId] ON [ActivityFindings] ([LegacyFindingId]) WHERE [LegacyFindingId] IS NOT NULL;

CREATE INDEX [IX_ActivityReviewEvents_InspectionActivityId_DecidedAt] ON [ActivityReviewEvents] ([InspectionActivityId], [DecidedAt]);

CREATE INDEX [IX_ActivityReviewEvents_ActorUserId] ON [ActivityReviewEvents] ([ActorUserId]);

CREATE INDEX [IX_ChecklistVersionSnapshots_PublishedByUserId] ON [ChecklistVersionSnapshots] ([PublishedByUserId]);

CREATE UNIQUE INDEX [IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber] ON [ChecklistVersionSnapshots] ([SourceChecklistTemplateId], [VersionNumber]);

CREATE UNIQUE INDEX [IX_CheckpointDefinitionSnapshots_ChecklistVersionSnapshotId_QuestionCode_QuestionVersion] ON [CheckpointDefinitionSnapshots] ([ChecklistVersionSnapshotId], [QuestionCode], [QuestionVersion]);

CREATE UNIQUE INDEX [IX_CheckpointResponseRevisions_ActivityRevisionId_CheckpointDefinitionSnapshotId_RevisionNumber] ON [CheckpointResponseRevisions] ([ActivityRevisionId], [CheckpointDefinitionSnapshotId], [RevisionNumber]);

CREATE INDEX [IX_CheckpointResponseRevisions_CheckpointDefinitionSnapshotId] ON [CheckpointResponseRevisions] ([CheckpointDefinitionSnapshotId]);

CREATE INDEX [IX_CheckpointResponseRevisions_RecordedByUserId] ON [CheckpointResponseRevisions] ([RecordedByUserId]);

CREATE INDEX [IX_CheckpointResponseRevisions_ReviewedByUserId] ON [CheckpointResponseRevisions] ([ReviewedByUserId]);

CREATE INDEX [IX_CorrectiveActionEvents_ActorUserId] ON [CorrectiveActionEvents] ([ActorUserId]);

CREATE INDEX [IX_CorrectiveActionEvents_CorrectiveActionId_OccurredAt] ON [CorrectiveActionEvents] ([CorrectiveActionId], [OccurredAt]);

CREATE UNIQUE INDEX [IX_CorrectiveActions_ActionReference] ON [CorrectiveActions] ([ActionReference]);

CREATE INDEX [IX_CorrectiveActions_ActivityFindingId_CreatedAt] ON [CorrectiveActions] ([ActivityFindingId], [CreatedAt]);

CREATE INDEX [IX_CorrectiveActions_AriesVerifierUserId] ON [CorrectiveActions] ([AriesVerifierUserId]);

CREATE INDEX [IX_CorrectiveActions_ClientDecisionUserId] ON [CorrectiveActions] ([ClientDecisionUserId]);

CREATE INDEX [IX_CorrectiveActions_ResponsibleUserId] ON [CorrectiveActions] ([ResponsibleUserId]);

CREATE INDEX [IX_InspectionActivities_ChecklistVersionId] ON [InspectionActivities] ([ChecklistVersionId]);

CREATE INDEX [IX_InspectionActivities_InspectionTaskId] ON [InspectionActivities] ([InspectionTaskId]);

CREATE INDEX [IX_InspectionActivities_InspectorUserId] ON [InspectionActivities] ([InspectorUserId]);

CREATE UNIQUE INDEX [IX_InspectionActivities_LegacySurveySessionId] ON [InspectionActivities] ([LegacySurveySessionId]) WHERE [LegacySurveySessionId] IS NOT NULL;

CREATE INDEX [IX_InspectionActivityRevisions_CreatedByUserId] ON [InspectionActivityRevisions] ([CreatedByUserId]);

CREATE UNIQUE INDEX [IX_InspectionActivityRevisions_InspectionActivityId_RevisionNumber] ON [InspectionActivityRevisions] ([InspectionActivityId], [RevisionNumber]);

CREATE INDEX [IX_InspectionActivityRevisions_InspectionActivityId1] ON [InspectionActivityRevisions] ([InspectionActivityId1]);

CREATE INDEX [IX_InspectionPlans_ProjectId] ON [InspectionPlans] ([ProjectId]);

CREATE INDEX [IX_InspectionTasks_AreaEquipmentId] ON [InspectionTasks] ([AreaEquipmentId]);

CREATE INDEX [IX_InspectionTasks_AssignedUserId] ON [InspectionTasks] ([AssignedUserId]);

CREATE INDEX [IX_InspectionTasks_InspectionPlanId_ScheduledAt] ON [InspectionTasks] ([InspectionPlanId], [ScheduledAt]);

CREATE INDEX [IX_InspectionTasks_LegacyEquipmentId] ON [InspectionTasks] ([LegacyEquipmentId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030104113_InspectionActivityFoundation', N'10.0.13');

COMMIT;
GO

